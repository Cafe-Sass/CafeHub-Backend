using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Identity;
using CafeHub.Domain.Identity;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CafeHub.UnitTests
{
    public sealed class TenantRefreshTokenServiceTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

        [Fact]
        public async Task Rotate_ReplacesTheTokenWithinItsFamily()
        {
            var (service, store, _, _, _) = CreateService("old-secret", "new-secret");
            var userId = Guid.NewGuid();
            var issued = await service.IssueAsync(userId, default);

            var result = await service.RotateAsync("old-secret", default);

            Assert.True(result.IsSuccess);
            Assert.Equal(userId, result.Value.UserId);
            Assert.Equal("new-secret", result.Value.RawToken);
            var oldToken = Assert.Single(store.Tokens, token => token.TokenHash == Hash("old-secret"));
            var replacement = Assert.Single(store.Tokens, token => token.TokenHash == Hash("new-secret"));
            Assert.Equal(replacement.Id, oldToken.ReplacedById);
            Assert.Equal(issued.Value.FamilyId, replacement.FamilyId);
            Assert.NotNull(oldToken.RotatedAt);
        }

        [Fact]
        public async Task Rotate_ReplayRevokesTheFamilyWithReuseDetected()
        {
            var (service, store, _, _, _) = CreateService("old-secret", "new-secret");
            await service.IssueAsync(Guid.NewGuid(), default);
            await service.RotateAsync("old-secret", default);

            var result = await service.RotateAsync("old-secret", default);

            AssertInvalid(result);
            Assert.All(store.Tokens, token => Assert.Equal(RefreshTokenRevocationReasons.ReuseDetected, token.RevokedReason));
        }

        [Fact]
        public async Task Rotate_ConcurrentDoubleUse_HasOneWinnerAndRevokesFamilyAfterLoserDetectsReuse()
        {
            var shared = new ConcurrentPersistence();
            var userId = Guid.NewGuid();
            var familyId = Guid.NewGuid();
            shared.Seed(TenantRefreshToken.Issue(Guid.NewGuid(), userId, familyId, Hash("old-secret"), Now, Now.AddDays(7)));
            var first = CreateConcurrentService(shared, "new-secret-1");
            var second = CreateConcurrentService(shared, "new-secret-2");

            var results = await Task.WhenAll(
                first.Service.RotateAsync("old-secret", default),
                second.Service.RotateAsync("old-secret", default));

            Assert.Single(results, result => result.IsSuccess);
            Assert.Single(results, result => result.IsFailure && result.Error == AuthErrors.InvalidRefreshToken());
            Assert.All(shared.Tokens, token => Assert.Equal(RefreshTokenRevocationReasons.ReuseDetected, token.RevokedReason));
        }

        [Fact]
        public async Task Rotate_UnknownExpiredRevokedAndMalformedTokensHaveTheSameError()
        {
            var (service, store, _, time, _) = CreateService("expired-secret", "revoked-secret");
            await service.IssueAsync(Guid.NewGuid(), default);
            time.UtcNow = Now.AddDays(8);
            var expired = await service.RotateAsync("expired-secret", default);
            time.UtcNow = Now.AddHours(1);
            await service.IssueAsync(Guid.NewGuid(), default);
            await service.RevokeFamilyAsync("revoked-secret", RefreshTokenRevocationReasons.Logout, default);

            Assert.Equal(AuthErrors.InvalidRefreshToken(), expired.Error);
            Assert.Equal(expired.Error, (await service.RotateAsync("unknown-secret", default)).Error);
            Assert.Equal(expired.Error, (await service.RotateAsync("revoked-secret", default)).Error);
            Assert.Equal(expired.Error, (await service.RotateAsync(string.Empty, default)).Error);
            Assert.Equal(expired.Error, (await service.RotateAsync(new string('x', 257), default)).Error);
        }

        [Fact]
        public async Task Rotate_CrossTenantToken_IsIndistinguishableFromUnknownToken()
        {
            var (issuingService, _, _, _, _) = CreateService("tenant-a-secret");
            await issuingService.IssueAsync(Guid.NewGuid(), default);
            var (otherTenantService, _, _, _, _) = CreateService();

            var crossTenant = await otherTenantService.RotateAsync("tenant-a-secret", default);
            var unknown = await otherTenantService.RotateAsync("unknown-secret", default);

            Assert.Equal(AuthErrors.InvalidRefreshToken(), crossTenant.Error);
            Assert.Equal(unknown.Error, crossTenant.Error);
        }

        [Fact]
        public async Task RevokeFamily_RevokesAllTokens_AndUnknownIsIdempotentlyRejected()
        {
            var (service, store, _, _, _) = CreateService("first-secret", "second-secret");
            var issue = await service.IssueAsync(Guid.NewGuid(), default);
            await service.RotateAsync("first-secret", default);

            var result = await service.RevokeFamilyAsync("second-secret", RefreshTokenRevocationReasons.Logout, default);
            var unknown = await service.RevokeFamilyAsync("unrecognized-secret", RefreshTokenRevocationReasons.Logout, default);

            Assert.True(result.IsSuccess);
            Assert.All(store.Tokens, token => Assert.Equal(RefreshTokenRevocationReasons.Logout, token.RevokedReason));
            Assert.False(unknown.IsSuccess);
            Assert.All(store.Tokens, token => Assert.Equal(issue.Value.FamilyId, token.FamilyId));
        }

        [Fact]
        public async Task RejectionsNeverLogRawOrHashedTokens()
        {
            var (service, _, _, _, logger) = CreateService("known-secret");
            await service.RotateAsync("unknown-secret", default);
            await service.RotateAsync("known-secret", default);

            Assert.NotEmpty(logger.Messages);
            foreach (var token in new[] { "known-secret", "unknown-secret" })
            {
                Assert.DoesNotContain(logger.Messages, message => message.Contains(token, StringComparison.Ordinal));
                Assert.DoesNotContain(logger.Messages, message => message.Contains(Hash(token), StringComparison.Ordinal));
            }
        }

        private static (TenantRefreshTokenService Service, FakeStore Store, FakeUnitOfWork UnitOfWork, MutableTimeProvider Time, TestLogger Logger) CreateService(params string[] tokens)
        {
            var store = new FakeStore();
            var unit = new FakeUnitOfWork();
            var time = new MutableTimeProvider { UtcNow = Now };
            var logger = new TestLogger();
            var service = new TenantRefreshTokenService(store, unit, new TokenGenerator(tokens), new TokenHasher(), new RefreshTokenOptions(), time, logger);
            return (service, store, unit, time, logger);
        }

        private static void AssertInvalid<T>(CafeHub.Application.Abstractions.Results.Result<T> result)
        {
            Assert.False(result.IsSuccess);
            Assert.Equal(AuthErrors.InvalidRefreshToken(), result.Error);
        }

        private static string Hash(string value) => new TokenHasher().Hash(value);

        private static (TenantRefreshTokenService Service, ConcurrentFakeStore Store) CreateConcurrentService(ConcurrentPersistence persistence, string replacementToken)
        {
            var store = new ConcurrentFakeStore(persistence);
            var unit = new ConcurrentFakeUnitOfWork(persistence, store);
            var logger = new TestLogger();
            var service = new TenantRefreshTokenService(store, unit, new TokenGenerator(replacementToken), new TokenHasher(), new RefreshTokenOptions(), new MutableTimeProvider { UtcNow = Now }, logger);
            return (service, store);
        }

        private sealed class FakeStore : ITenantRefreshTokenStore
        {
            public List<TenantRefreshToken> Tokens { get; } = [];
            public Task<TenantRefreshToken?> FindByTokenHashAsync(string hash, CancellationToken cancellationToken) => Task.FromResult(Tokens.SingleOrDefault(token => token.TokenHash == hash));
            public Task<IReadOnlyList<TenantRefreshToken>> FindFamilyAsync(Guid familyId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<TenantRefreshToken>>(Tokens.Where(token => token.FamilyId == familyId).ToArray());
            public void Add(TenantRefreshToken token) => Tokens.Add(token);
            public void ClearTracking() { }
        }

        private sealed class FakeUnitOfWork : ITenantUnitOfWork
        {
            private readonly Queue<Exception> commitFailures = new();
            public int BeginCount { get; private set; }
            public int RollbackCount { get; private set; }
            public void ThrowOnNextCommit(Exception exception) => commitFailures.Enqueue(exception);
            public Task BeginTransactionAsync(CancellationToken cancellationToken) { BeginCount++; return Task.CompletedTask; }
            public Task CommitTransactionAsync(CancellationToken cancellationToken) => commitFailures.Count == 0 ? Task.CompletedTask : Task.FromException(commitFailures.Dequeue());
            public Task RollbackTransactionAsync(CancellationToken cancellationToken) { RollbackCount++; return Task.CompletedTask; }
        }

        private sealed class TokenGenerator(params string[] tokens) : IRefreshTokenGenerator
        {
            private readonly Queue<string> remaining = new(tokens);
            public string Generate() => remaining.Dequeue();
        }

        private sealed class MutableTimeProvider : TimeProvider
        {
            public DateTimeOffset UtcNow { get; set; }
            public override DateTimeOffset GetUtcNow() => UtcNow;
        }

        private sealed class TestLogger : ILogger<TenantRefreshTokenService>
        {
            public List<string> Messages { get; } = [];
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
        }

        private sealed class ConcurrentPersistence
        {
            private readonly object sync = new();
            private readonly List<TenantRefreshToken> tokens = [];
            private readonly Barrier lookupBarrier = new(2);

            public IReadOnlyList<TenantRefreshToken> Tokens { get { lock (sync) return tokens.ToArray(); } }

            public void Seed(TenantRefreshToken token) { lock (sync) tokens.Add(token); }

            public List<TenantRefreshToken> Snapshot()
            {
                lock (sync) return tokens.Select(Clone).ToList();
            }

            public Task SynchronizeInitialLookupAsync() => Task.Run(() => lookupBarrier.SignalAndWait(TimeSpan.FromSeconds(10)));

            public void Commit(IReadOnlyList<TenantRefreshToken> changes)
            {
                lock (sync)
                {
                    var rotations = changes.Where(token => token.RotatedAt is not null && token.RevokedAt is null);
                    if (rotations.Any(rotation => tokens.Single(token => token.Id == rotation.Id).RotatedAt is not null))
                    {
                        throw new DbUpdateConcurrencyException();
                    }

                    foreach (var changed in changes)
                    {
                        var index = tokens.FindIndex(token => token.Id == changed.Id);
                        if (index >= 0) tokens[index] = Clone(changed);
                        else tokens.Add(Clone(changed));
                    }
                }
            }

            private static TenantRefreshToken Clone(TenantRefreshToken source)
            {
                var clone = TenantRefreshToken.Issue(source.Id, source.UserId, source.FamilyId, source.TokenHash, source.IssuedAt, source.ExpiresAt);
                if (source.RotatedAt is { } rotatedAt && source.ReplacedById is { } replacementId) clone.Rotate(rotatedAt, replacementId);
                if (source.RevokedAt is { } revokedAt && source.RevokedReason is { } reason) clone.Revoke(revokedAt, reason);
                return clone;
            }
        }

        private sealed class ConcurrentFakeStore(ConcurrentPersistence persistence) : ITenantRefreshTokenStore
        {
            private List<TenantRefreshToken>? localTokens;
            public IReadOnlyList<TenantRefreshToken> Changes => localTokens ?? [];

            public async Task<TenantRefreshToken?> FindByTokenHashAsync(string hash, CancellationToken cancellationToken)
            {
                localTokens ??= persistence.Snapshot();
                await persistence.SynchronizeInitialLookupAsync();
                return localTokens.SingleOrDefault(token => token.TokenHash == hash);
            }

            public Task<IReadOnlyList<TenantRefreshToken>> FindFamilyAsync(Guid familyId, CancellationToken cancellationToken)
            {
                localTokens ??= persistence.Snapshot();
                return Task.FromResult<IReadOnlyList<TenantRefreshToken>>(localTokens.Where(token => token.FamilyId == familyId).ToArray());
            }

            public void Add(TenantRefreshToken token) { localTokens ??= persistence.Snapshot(); localTokens.Add(token); }
            public void ClearTracking() => localTokens = null;
        }

        private sealed class ConcurrentFakeUnitOfWork(ConcurrentPersistence persistence, ConcurrentFakeStore store) : ITenantUnitOfWork
        {
            public Task BeginTransactionAsync(CancellationToken cancellationToken) => Task.CompletedTask;
            public Task CommitTransactionAsync(CancellationToken cancellationToken) { persistence.Commit(store.Changes); return Task.CompletedTask; }
            public Task RollbackTransactionAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        }
    }
}
