using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Domain.Identity;
using CafeHub.Infrastructure.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CafeHub.UnitTests
{
    public sealed class RefreshTokenServiceTests
    {
        private static readonly DateTimeOffset FixedNow = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

        private const string FirstRawToken = "raw-token-first";
        private const string SecondRawToken = "raw-token-second";
        private const string UnknownRawToken = "raw-token-unknown";

        private readonly TokenHasher hasher = new();

        private static (RefreshTokenService Service, FakeRefreshTokenStore Store, FakeMasterUnitOfWork UnitOfWork, MutableTimeProvider TimeProvider, ListLogger Logger) CreateService(params string[] generatedTokens)
        {
            var store = new FakeRefreshTokenStore();
            var unitOfWork = new FakeMasterUnitOfWork();
            var generator = new FakeRefreshTokenGenerator(generatedTokens);
            var timeProvider = new MutableTimeProvider { UtcNow = FixedNow };
            var logger = new ListLogger();
            var service = new RefreshTokenService(
                store,
                unitOfWork,
                generator,
                new TokenHasher(),
                new RefreshTokenOptions(),
                timeProvider,
                logger);

            return (service, store, unitOfWork, timeProvider, logger);
        }

        private async Task<IssuedRefreshToken> IssueToken(RefreshTokenService service, Guid adminId)
        {
            var result = await service.IssueAsync(adminId, CancellationToken.None);
            Assert.True(result.IsSuccess);
            return result.Value;
        }

        [Fact]
        public async Task Issue_StoresOnlyTheHash_AndReturnsTheRawValueOnce()
        {
            var adminId = Guid.NewGuid();
            var (service, store, _, timeProvider, _) = CreateService(FirstRawToken);

            var result = await service.IssueAsync(adminId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(FirstRawToken, result.Value.RawToken);
            Assert.Equal(FixedNow.AddDays(7), result.Value.ExpiresAt);
            var stored = Assert.Single(store.Tokens);
            Assert.Equal(adminId, stored.AdminId);
            Assert.Equal(hasher.Hash(FirstRawToken), stored.TokenHash);
            Assert.Equal(result.Value.FamilyId, stored.FamilyId);
            Assert.Equal(timeProvider.UtcNow, stored.IssuedAt);
        }

        [Fact]
        public async Task Issue_WithEmptyAdminId_Throws()
        {
            var (service, _, _, _, _) = CreateService(FirstRawToken);

            await Assert.ThrowsAsync<ArgumentException>(() => service.IssueAsync(Guid.Empty, CancellationToken.None));
        }

        [Fact]
        public async Task Rotate_MarksTheOldTokenRotated_AndIssuesTheSuccessorInTheSameFamily()
        {
            var adminId = Guid.NewGuid();
            var (service, store, _, timeProvider, _) = CreateService(FirstRawToken, SecondRawToken);
            var issued = await IssueToken(service, adminId);
            timeProvider.UtcNow = FixedNow.AddHours(1);

            var result = await service.RotateAsync(FirstRawToken, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(SecondRawToken, result.Value.RawToken);
            Assert.Equal(adminId, result.Value.AdminId);
            Assert.Equal(FixedNow.AddHours(1).AddDays(7), result.Value.ExpiresAt);
            var oldToken = store.Tokens.Single(token => token.TokenHash == hasher.Hash(FirstRawToken));
            Assert.Equal(FixedNow.AddHours(1), oldToken.RotatedAt);
            var newToken = store.Tokens.Single(token => token.TokenHash == hasher.Hash(SecondRawToken));
            Assert.Equal(oldToken.ReplacedById, newToken.Id);
            Assert.Equal(oldToken.FamilyId, newToken.FamilyId);
            Assert.Equal(oldToken.FamilyId, issued.FamilyId);
        }

        [Fact]
        public async Task Rotate_AnOldRawValue_NoLongerRotatesSuccessfully_AndRevokesTheFamily()
        {
            var (service, store, _, _, _) = CreateService(FirstRawToken, SecondRawToken);
            await IssueToken(service, Guid.NewGuid());
            (await service.RotateAsync(FirstRawToken, CancellationToken.None)).AssertSuccess();

            var replay = await service.RotateAsync(FirstRawToken, CancellationToken.None);

            Assert.True(replay.IsFailure);
            Assert.Equal(RefreshTokenService.InvalidRefreshTokenCode, replay.Error!.Code);
            Assert.All(store.Tokens, token => Assert.NotNull(token.RevokedAt));
            Assert.All(store.Tokens, token => Assert.Equal(RefreshTokenRevocationReasons.ReuseDetected, token.RevokedReason));
        }

        [Fact]
        public async Task InvalidCases_AllReturnTheIdenticalSafeError()
        {
            var (service, store, _, _, _) = CreateService(FirstRawToken, SecondRawToken);
            var adminId = Guid.NewGuid();
            var familyId = Guid.NewGuid();
            var expiredToken = RefreshToken.Issue(Guid.CreateVersion7(), adminId, familyId, hasher.Hash("expired-raw"), FixedNow.AddDays(-8), FixedNow.AddDays(-1));
            var revokedToken = RefreshToken.Issue(Guid.CreateVersion7(), adminId, familyId, hasher.Hash("revoked-raw"), FixedNow.AddDays(-8), FixedNow.AddDays(7));
            revokedToken.Revoke(FixedNow.AddHours(-1), RefreshTokenRevocationReasons.AdminRevoked);
            store.Tokens.AddRange(new[] { expiredToken, revokedToken });

            var unknown = await service.RotateAsync(UnknownRawToken, CancellationToken.None);
            var empty = await service.RotateAsync("", CancellationToken.None);
            var whitespace = await service.RotateAsync("   ", CancellationToken.None);
            var oversized = await service.RotateAsync(new string('x', 600), CancellationToken.None);
            var expired = await service.RotateAsync("expired-raw", CancellationToken.None);
            var revoked = await service.RotateAsync("revoked-raw", CancellationToken.None);

            foreach (var result in new[] { unknown, empty, whitespace, oversized, expired, revoked })
            {
                Assert.True(result.IsFailure);
                Assert.Equal(RefreshTokenService.InvalidRefreshTokenCode, result.Error!.Code);
                Assert.Equal(RefreshTokenService.InvalidRefreshTokenMessage, result.Error.Message);
            }
        }

        [Fact]
        public async Task RevokeFamily_SetsTheReason_OnEveryFamilyToken_AndIsIdempotent()
        {
            var (service, store, _, _, _) = CreateService(FirstRawToken, SecondRawToken);
            await IssueToken(service, Guid.NewGuid());
            (await service.RotateAsync(FirstRawToken, CancellationToken.None)).AssertSuccess();

            var firstRevoke = await service.RevokeFamilyAsync(SecondRawToken, RefreshTokenRevocationReasons.AdminRevoked, CancellationToken.None);
            var secondRevoke = await service.RevokeFamilyAsync(SecondRawToken, RefreshTokenRevocationReasons.AdminRevoked, CancellationToken.None);

            Assert.True(firstRevoke.IsSuccess);
            Assert.True(secondRevoke.IsSuccess);
            Assert.All(store.Tokens, token => Assert.Equal(RefreshTokenRevocationReasons.AdminRevoked, token.RevokedReason));
        }

        [Fact]
        public async Task RevokeFamily_WithAnUnknownToken_ReturnsTheInvalidRefreshTokenError()
        {
            var (service, _, _, _, _) = CreateService(FirstRawToken);

            var result = await service.RevokeFamilyAsync(UnknownRawToken, RefreshTokenRevocationReasons.Logout, CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(RefreshTokenService.InvalidRefreshTokenCode, result.Error!.Code);
            Assert.Equal(RefreshTokenService.InvalidRefreshTokenMessage, result.Error.Message);
        }

        [Fact]
        public async Task RevokeFamily_WithAnUndocumentedReason_Throws()
        {
            var (service, _, _, _, _) = CreateService(FirstRawToken);
            await IssueToken(service, Guid.NewGuid());

            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.RevokeFamilyAsync(FirstRawToken, "hacked", CancellationToken.None));
        }

        [Fact]
        public async Task AConcurrentRotationLoser_IsTreatedAsReuse_AndRevokesTheFamily()
        {
            var (service, store, unitOfWork, _, _) = CreateService(FirstRawToken, SecondRawToken);
            await IssueToken(service, Guid.NewGuid());
            unitOfWork.ThrowOnNextCommit(new DbUpdateConcurrencyException("The row was modified concurrently."));

            var result = await service.RotateAsync(FirstRawToken, CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(RefreshTokenService.InvalidRefreshTokenCode, result.Error!.Code);
            Assert.Equal(1, unitOfWork.RollbackCount);
            Assert.All(store.Tokens, token => Assert.Equal(RefreshTokenRevocationReasons.ReuseDetected, token.RevokedReason));
        }

        [Fact]
        public async Task Rejections_NeverLogTheRawTokenOrItsHash()
        {
            var (service, store, _, _, logger) = CreateService(FirstRawToken, SecondRawToken);
            await IssueToken(service, Guid.NewGuid());
            (await service.RotateAsync(FirstRawToken, CancellationToken.None)).AssertSuccess();

            await service.RotateAsync(UnknownRawToken, CancellationToken.None);
            await service.RotateAsync(FirstRawToken, CancellationToken.None);
            await service.RevokeFamilyAsync(SecondRawToken, RefreshTokenRevocationReasons.AdminRevoked, CancellationToken.None);

            Assert.NotEmpty(logger.Entries);
            foreach (var rawToken in new[] { FirstRawToken, SecondRawToken, UnknownRawToken })
            {
                Assert.DoesNotContain(rawToken, logger.CapturedText, StringComparison.Ordinal);
                Assert.DoesNotContain(hasher.Hash(rawToken), logger.CapturedText, StringComparison.Ordinal);
            }
        }

    }

    internal static class RefreshTokenServiceTestExtensions
    {
        public static void AssertSuccess(this Result result)
        {
            Assert.True(result.IsSuccess);
        }
    }

    internal sealed class FakeRefreshTokenStore : IRefreshTokenStore
    {
        public List<RefreshToken> Tokens { get; } = new();

        public Task<RefreshToken?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
        {
            return Task.FromResult<RefreshToken?>(Tokens.SingleOrDefault(token => token.TokenHash == tokenHash));
        }

        public Task<IReadOnlyList<RefreshToken>> FindFamilyAsync(Guid familyId, CancellationToken cancellationToken)
        {
            IReadOnlyList<RefreshToken> family = Tokens.Where(token => token.FamilyId == familyId).ToList();

            return Task.FromResult(family);
        }

        public void Add(RefreshToken token)
        {
            Tokens.Add(token);
        }
    }

    internal sealed class FakeMasterUnitOfWork : IMasterUnitOfWork
    {
        private readonly Queue<Exception> commitExceptions = new();

        public int BeginCount { get; private set; }

        public int CommitCount { get; private set; }

        public int RollbackCount { get; private set; }

        public void ThrowOnNextCommit(Exception exception)
        {
            commitExceptions.Enqueue(exception);
        }

        public Task BeginTransactionAsync(CancellationToken cancellationToken)
        {
            BeginCount++;
            return Task.CompletedTask;
        }

        public Task CommitTransactionAsync(CancellationToken cancellationToken)
        {
            CommitCount++;

            if (commitExceptions.Count > 0)
            {
                throw commitExceptions.Dequeue();
            }

            return Task.CompletedTask;
        }

        public Task RollbackTransactionAsync(CancellationToken cancellationToken)
        {
            RollbackCount++;
            return Task.CompletedTask;
        }
    }

    internal sealed class FakeRefreshTokenGenerator : IRefreshTokenGenerator
    {
        private readonly Queue<string> tokens;

        public FakeRefreshTokenGenerator(params string[] tokens)
        {
            this.tokens = new Queue<string>(tokens);
        }

        public string Generate()
        {
            return tokens.Dequeue();
        }
    }

    internal sealed class MutableTimeProvider : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; }

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    internal sealed record RefreshTokenLogEntry(LogLevel Level, string Message, Exception? Exception);

    internal sealed class ListLogger : ILogger<RefreshTokenService>
    {
        public List<RefreshTokenLogEntry> Entries { get; } = new();

        public string CapturedText => string.Join("\n", Entries.Select(entry => entry.Message));

        private sealed class NoopDisposable : IDisposable
        {
            public static readonly NoopDisposable Instance = new();

            public void Dispose()
            {
            }
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return NoopDisposable.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new RefreshTokenLogEntry(logLevel, formatter(state, exception), exception));
        }
    }
}
