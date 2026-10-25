using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Identity;
using CafeHub.Application.Identity.Commands;
using CafeHub.Application.Identity.DTOs;
using CafeHub.Domain.Identity;

namespace CafeHub.UnitTests
{
    public sealed class LoginAdminCommandHandlerTests
    {
        private const string CommandEmail = "admin.email@cafehub.app";
        private const string CommandPassword = "the-submitted-password";
        private const string SucceededAuditAction = "admin.login.succeeded";
        private const string FailedAuditAction = "admin.login.failed";
        private const string PlatformAdminTargetType = "platform_admin";

        private readonly HandlerCallLog callLog = new();
        private readonly StubAdminAuthenticator authenticator;
        private readonly StubAccessTokenIssuer accessTokenIssuer;
        private readonly StubRefreshTokenService refreshTokenService;
        private readonly RecordingAuditWriter auditWriter;
        private readonly RecordingAuditFlushUnitOfWork unitOfWork;
        private readonly LoginAdminCommandHandler handler;

        public LoginAdminCommandHandlerTests()
        {
            authenticator = new StubAdminAuthenticator();
            accessTokenIssuer = new StubAccessTokenIssuer();
            refreshTokenService = new StubRefreshTokenService();
            auditWriter = new RecordingAuditWriter(callLog);
            unitOfWork = new RecordingAuditFlushUnitOfWork(callLog);
            handler = new LoginAdminCommandHandler(authenticator, accessTokenIssuer, refreshTokenService, auditWriter, unitOfWork);
        }

        private static Result<AuthenticatedAdmin> SuccessfulAuthentication(Guid adminId)
        {
            return Result.Success(new AuthenticatedAdmin
            {
                Id = adminId,
                Email = CommandEmail,
                Roles = new[] { PlatformAdminRoles.SuperAdmin },
            });
        }

        private Task<Result<AdminTokenResponse>> HandleLoginAsync()
        {
            return handler.Handle(new LoginAdminCommand(CommandEmail, CommandPassword), CancellationToken.None);
        }

        [Fact]
        public async Task TheLoginCommand_IsForwardedToTheAuthenticator()
        {
            authenticator.NextResult = SuccessfulAuthentication(Guid.NewGuid());

            await HandleLoginAsync();

            Assert.Equal(CommandEmail, authenticator.LastEmail);
            Assert.Equal(CommandPassword, authenticator.LastPassword);
        }

        [Fact]
        public async Task SuccessfulCredentials_IssueBothTokens_AndPassTheAdminIdentityToTheirSources()
        {
            var adminId = Guid.NewGuid();
            authenticator.NextResult = SuccessfulAuthentication(adminId);

            var result = await HandleLoginAsync();

            Assert.True(result.IsSuccess);
            var response = result.Value!;
            Assert.Equal(accessTokenIssuer.NextToken.Value, response.AccessToken);
            Assert.Equal(accessTokenIssuer.NextToken.ExpiresAt, response.AccessTokenExpiresAt);
            Assert.Equal(refreshTokenService.NextIssue.RawToken, response.RefreshToken);
            Assert.Equal(refreshTokenService.NextIssue.ExpiresAt, response.RefreshTokenExpiresAt);
            var request = Assert.Single(accessTokenIssuer.Requests);
            Assert.Equal(adminId, request.AdministratorId);
            Assert.Equal(CommandEmail, request.Email);
            Assert.Equal(new[] { PlatformAdminRoles.SuperAdmin }, request.Roles);
            Assert.Equal(adminId, Assert.Single(refreshTokenService.IssuedAdminIds));
        }

        [Fact]
        public async Task SuccessfulCredentials_WriteASucceededAuditEntry_AndFlushItImmediately()
        {
            var adminId = Guid.NewGuid();
            authenticator.NextResult = SuccessfulAuthentication(adminId);

            var result = await HandleLoginAsync();

            Assert.True(result.IsSuccess);
            var entry = Assert.Single(auditWriter.Entries);
            Assert.Equal(PlatformAuditActorKind.System, entry.ActorKind);
            Assert.Equal(SucceededAuditAction, entry.Action);
            Assert.Equal(PlatformAdminTargetType, entry.TargetType);
            Assert.Equal(adminId, entry.TargetId);
            Assert.Equal(PlatformAuditOutcome.Succeeded, entry.Outcome);
            Assert.Null(entry.Reason);
            Assert.Equal(1, unitOfWork.BeginCount);
            Assert.Equal(1, unitOfWork.CommitCount);
            Assert.Equal(new[] { SucceededAuditAction, HandlerCallLog.BeginStep, HandlerCallLog.CommitStep }, callLog.Steps);
        }

        [Fact]
        public async Task AnUnknownEmail_Rejection_ReturnsTheSafeError_WritesTheFailedAudit_AndFlushesIt()
        {
            authenticator.NextResult = Result.Failure<AuthenticatedAdmin>(AuthErrors.InvalidCredentials());

            var result = await HandleLoginAsync();

            Assert.True(result.IsFailure);
            Assert.Equal(AuthErrors.InvalidCredentialsCode, result.Error!.Code);
            Assert.Equal(AuthErrors.InvalidCredentialsMessage, result.Error.Message);
            Assert.Equal(ErrorType.Unauthorized, result.Error.Type);
            var entry = Assert.Single(auditWriter.Entries);
            Assert.Equal(PlatformAuditActorKind.System, entry.ActorKind);
            Assert.Equal(FailedAuditAction, entry.Action);
            Assert.Equal(PlatformAdminTargetType, entry.TargetType);
            Assert.Null(entry.TargetId);
            Assert.Equal(PlatformAuditOutcome.Failed, entry.Outcome);
            Assert.Equal("invalid_credentials", entry.Reason);
            Assert.Equal(1, unitOfWork.BeginCount);
            Assert.Equal(1, unitOfWork.CommitCount);
            Assert.Equal(new[] { FailedAuditAction, HandlerCallLog.BeginStep, HandlerCallLog.CommitStep }, callLog.Steps);
            Assert.Empty(accessTokenIssuer.Requests);
            Assert.Empty(refreshTokenService.IssuedAdminIds);
        }

        [Fact]
        public async Task ARejectedKnownAdmin_IsAuditedWithTheInvalidCredentialsReason_AndItsAdminId()
        {
            var adminId = Guid.NewGuid();
            authenticator.NextResult = Result.Failure<AuthenticatedAdmin>(RejectedAdminCredentials.Create(adminId));

            var result = await HandleLoginAsync();

            Assert.True(result.IsFailure);
            Assert.Equal(AuthErrors.InvalidCredentialsCode, result.Error!.Code);
            Assert.Equal(AuthErrors.InvalidCredentialsMessage, result.Error.Message);
            Assert.Equal(ErrorType.Unauthorized, result.Error.Type);
            var entry = Assert.Single(auditWriter.Entries);
            Assert.Equal(PlatformAuditActorKind.System, entry.ActorKind);
            Assert.Equal(FailedAuditAction, entry.Action);
            Assert.Equal(PlatformAdminTargetType, entry.TargetType);
            Assert.Equal(adminId, entry.TargetId);
            Assert.Equal(PlatformAuditOutcome.Failed, entry.Outcome);
            Assert.Equal("invalid_credentials", entry.Reason);
            Assert.Equal(new[] { FailedAuditAction, HandlerCallLog.BeginStep, HandlerCallLog.CommitStep }, callLog.Steps);
            Assert.Equal(1, unitOfWork.BeginCount);
            Assert.Equal(1, unitOfWork.CommitCount);
            Assert.Empty(accessTokenIssuer.Requests);
            Assert.Empty(refreshTokenService.IssuedAdminIds);
        }

        [Fact]
        public async Task ALockedOutAdmin_IsAuditedWithTheLockedOutReason_AndItsAdminId()
        {
            var adminId = Guid.NewGuid();
            authenticator.NextResult = Result.Failure<AuthenticatedAdmin>(LockedOutAdminCredentials.Create(adminId));

            var result = await HandleLoginAsync();

            Assert.True(result.IsFailure);
            Assert.Equal(AuthErrors.InvalidCredentialsCode, result.Error!.Code);
            Assert.Equal(AuthErrors.InvalidCredentialsMessage, result.Error.Message);
            Assert.Equal(ErrorType.Unauthorized, result.Error.Type);
            var entry = Assert.Single(auditWriter.Entries);
            Assert.Equal(PlatformAuditActorKind.System, entry.ActorKind);
            Assert.Equal(FailedAuditAction, entry.Action);
            Assert.Equal(PlatformAdminTargetType, entry.TargetType);
            Assert.Equal(adminId, entry.TargetId);
            Assert.Equal(PlatformAuditOutcome.Failed, entry.Outcome);
            Assert.Equal("locked_out", entry.Reason);
            Assert.Equal(new[] { FailedAuditAction, HandlerCallLog.BeginStep, HandlerCallLog.CommitStep }, callLog.Steps);
            Assert.Equal(1, unitOfWork.BeginCount);
            Assert.Equal(1, unitOfWork.CommitCount);
            Assert.Empty(accessTokenIssuer.Requests);
            Assert.Empty(refreshTokenService.IssuedAdminIds);
        }

        [Fact]
        public async Task EveryAuthenticationFailure_PresentsTheIdenticalClientVisibleError_AndAuditsOnlyReasonCodes()
        {
            const string knownEmail = "active.admin@cafehub.app";
            var adminId = Guid.NewGuid();
            var rejections = new Dictionary<string, Error>(StringComparer.Ordinal)
            {
                ["unknown-email"] = AuthErrors.InvalidCredentials(),
                ["wrong-password"] = RejectedAdminCredentials.Create(adminId),
                ["inactive-admin"] = RejectedAdminCredentials.Create(adminId),
                ["locked-out"] = LockedOutAdminCredentials.Create(adminId),
            };

            foreach (var scenario in rejections)
            {
                ResetScenarioState();
                authenticator.NextResult = Result.Failure<AuthenticatedAdmin>(scenario.Value);

                var result = await HandleLoginAsync();

                Assert.True(result.IsFailure);
                Assert.Equal(AuthErrors.InvalidCredentialsCode, result.Error!.Code);
                Assert.Equal(AuthErrors.InvalidCredentialsMessage, result.Error.Message);
                Assert.Equal(ErrorType.Unauthorized, result.Error.Type);
                var entry = Assert.Single(auditWriter.Entries);
                Assert.Contains(entry.Reason, new[] { "invalid_credentials", "locked_out" });
                Assert.DoesNotContain(knownEmail, entry.Reason, StringComparison.Ordinal);
                Assert.DoesNotContain(CommandPassword, entry.Reason, StringComparison.Ordinal);
            }
        }

        private void ResetScenarioState()
        {
            auditWriter.Entries.Clear();
            callLog.Steps.Clear();
        }
    }

    internal sealed class HandlerCallLog
    {
        public const string BeginStep = "begin";

        public const string CommitStep = "commit";

        public List<string> Steps { get; } = new();
    }

    internal sealed class StubAdminAuthenticator : IAdminAuthenticator
    {
        public Result<AuthenticatedAdmin> NextResult { get; set; } =
            Result.Failure<AuthenticatedAdmin>(AuthErrors.InvalidCredentials());

        public string? LastEmail { get; private set; }

        public string? LastPassword { get; private set; }

        public Task<Result<AuthenticatedAdmin>> CheckCredentialsAsync(string email, string password, CancellationToken cancellationToken)
        {
            LastEmail = email;
            LastPassword = password;

            return Task.FromResult(NextResult);
        }
    }

    internal sealed class StubAccessTokenIssuer : IAccessTokenIssuer
    {
        public AccessToken NextToken { get; set; } = new()
        {
            Value = "stub-access-token-value",
            ExpiresAt = new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero),
        };

        public List<AccessTokenRequest> Requests { get; } = new();

        public AccessToken Issue(AccessTokenRequest request)
        {
            Requests.Add(request);

            return NextToken;
        }
    }

    internal sealed class StubRefreshTokenService : IRefreshTokenService
    {
        public IssuedRefreshToken NextIssue { get; set; } = new()
        {
            RawToken = "stub-refresh-token-value",
            ExpiresAt = new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero),
            FamilyId = new Guid("0f0ee66a-1c7b-408b-b63d-1234567890ab"),
        };

        public List<Guid> IssuedAdminIds { get; } = new();

        public Task<Result<IssuedRefreshToken>> IssueAsync(Guid adminId, CancellationToken cancellationToken)
        {
            IssuedAdminIds.Add(adminId);

            return Task.FromResult(Result.Success(NextIssue));
        }

        public Task<Result<RotatedRefreshToken>> RotateAsync(string rawToken, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<Result<Guid>> RevokeFamilyAsync(string rawToken, string reason, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    internal sealed class RecordingAuditWriter : IPlatformAuditWriter
    {
        private readonly HandlerCallLog callLog;

        public RecordingAuditWriter(HandlerCallLog callLog)
        {
            this.callLog = callLog;
        }

        public List<PlatformAuditEntry> Entries { get; } = new();

        public void Write(PlatformAuditEntry entry)
        {
            Entries.Add(entry);
            callLog.Steps.Add(entry.Action);
        }
    }

    internal sealed class RecordingAuditFlushUnitOfWork : IMasterUnitOfWork
    {
        private readonly HandlerCallLog callLog;

        public RecordingAuditFlushUnitOfWork(HandlerCallLog callLog)
        {
            this.callLog = callLog;
        }

        public int BeginCount { get; private set; }

        public int CommitCount { get; private set; }

        public Task BeginTransactionAsync(CancellationToken cancellationToken)
        {
            BeginCount++;
            callLog.Steps.Add(HandlerCallLog.BeginStep);

            return Task.CompletedTask;
        }

        public Task CommitTransactionAsync(CancellationToken cancellationToken)
        {
            CommitCount++;
            callLog.Steps.Add(HandlerCallLog.CommitStep);

            return Task.CompletedTask;
        }

        public Task RollbackTransactionAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
