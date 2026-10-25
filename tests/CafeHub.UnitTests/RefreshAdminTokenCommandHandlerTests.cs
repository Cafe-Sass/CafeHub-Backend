using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Identity;
using CafeHub.Application.Identity.Commands;
using CafeHub.Application.Identity.DTOs;
using CafeHub.Domain.Identity;

namespace CafeHub.UnitTests
{
    public sealed class RefreshAdminTokenCommandHandlerTests
    {
        private const string SubmittedRefreshToken = "submitted-refresh-token";
        private const string RotatedRefreshTokenValue = "rotated-refresh-token";
        private const string AdminEmail = "refresh.admin@cafehub.app";
        private const string SucceededAuditAction = "admin.refresh.succeeded";
        private const string FailedAuditAction = "admin.refresh.failed";
        private const string PlatformAdminTargetType = "platform_admin";

        private static readonly DateTimeOffset RotatedExpiresAt = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

        private readonly HandlerCallLog callLog = new();
        private readonly SessionRefreshTokenServiceStub refreshTokenService;
        private readonly StubAdminAccountReader adminAccountReader;
        private readonly StubAccessTokenIssuer accessTokenIssuer;
        private readonly RecordingAuditWriter auditWriter;
        private readonly RecordingAuditFlushUnitOfWork unitOfWork;
        private readonly RefreshAdminTokenCommandHandler handler;

        public RefreshAdminTokenCommandHandlerTests()
        {
            refreshTokenService = new SessionRefreshTokenServiceStub();
            adminAccountReader = new StubAdminAccountReader();
            accessTokenIssuer = new StubAccessTokenIssuer();
            auditWriter = new RecordingAuditWriter(callLog);
            unitOfWork = new RecordingAuditFlushUnitOfWork(callLog);
            handler = new RefreshAdminTokenCommandHandler(
                refreshTokenService,
                adminAccountReader,
                accessTokenIssuer,
                auditWriter,
                unitOfWork);
        }

        private static AuthenticatedAdmin CreateAdmin(Guid adminId)
        {
            return new AuthenticatedAdmin
            {
                Id = adminId,
                Email = AdminEmail,
                Roles = new[] { PlatformAdminRoles.SuperAdmin },
            };
        }

        private void SetSuccessfulRotation(Guid adminId)
        {
            refreshTokenService.NextRotation = Result.Success(new RotatedRefreshToken
            {
                RawToken = RotatedRefreshTokenValue,
                ExpiresAt = RotatedExpiresAt,
                AdminId = adminId,
            });
        }

        private Task<Result<AdminTokenResponse>> HandleRefreshAsync()
        {
            return handler.Handle(new RefreshAdminTokenCommand(SubmittedRefreshToken), CancellationToken.None);
        }

        [Fact]
        public async Task TheRefreshToken_IsForwardedToTheRotationService()
        {
            var adminId = Guid.NewGuid();
            SetSuccessfulRotation(adminId);
            adminAccountReader.NextAdmin = CreateAdmin(adminId);

            await HandleRefreshAsync();

            Assert.Equal(new[] { SubmittedRefreshToken }, refreshTokenService.RotatedRawTokens);
        }

        [Fact]
        public async Task AValidRefreshToken_ReturnsTheRotatedTokenPair_AndIssuesTheAccessTokenForTheStoredIdentity()
        {
            var adminId = Guid.NewGuid();
            SetSuccessfulRotation(adminId);
            adminAccountReader.NextAdmin = CreateAdmin(adminId);

            var result = await HandleRefreshAsync();

            Assert.True(result.IsSuccess);
            var response = result.Value!;
            Assert.Equal(accessTokenIssuer.NextToken.Value, response.AccessToken);
            Assert.Equal(accessTokenIssuer.NextToken.ExpiresAt, response.AccessTokenExpiresAt);
            Assert.Equal(RotatedRefreshTokenValue, response.RefreshToken);
            Assert.Equal(RotatedExpiresAt, response.RefreshTokenExpiresAt);
            var request = Assert.Single(accessTokenIssuer.Requests);
            Assert.Equal(adminId, request.AdministratorId);
            Assert.Equal(AdminEmail, request.Email);
            Assert.Equal(new[] { PlatformAdminRoles.SuperAdmin }, request.Roles);
            Assert.Equal(new[] { adminId }, adminAccountReader.RequestedAdminIds);
            Assert.Empty(refreshTokenService.Revocations);
        }

        [Fact]
        public async Task AValidRefreshToken_WritesASucceededAuditEntry_AndFlushesIt()
        {
            var adminId = Guid.NewGuid();
            SetSuccessfulRotation(adminId);
            adminAccountReader.NextAdmin = CreateAdmin(adminId);

            var result = await HandleRefreshAsync();

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
        public async Task AFailedRotation_ReturnsTheSafeInvalidRefreshTokenError_AndAuditsTheFailure()
        {
            refreshTokenService.NextRotation = Result.Failure<RotatedRefreshToken>(AuthErrors.InvalidRefreshToken());

            var result = await HandleRefreshAsync();

            Assert.True(result.IsFailure);
            Assert.Equal(AuthErrors.InvalidRefreshTokenCode, result.Error!.Code);
            Assert.Equal(AuthErrors.InvalidRefreshTokenMessage, result.Error.Message);
            Assert.Equal(ErrorType.Unauthorized, result.Error.Type);
            var entry = Assert.Single(auditWriter.Entries);
            Assert.Equal(PlatformAuditActorKind.System, entry.ActorKind);
            Assert.Equal(FailedAuditAction, entry.Action);
            Assert.Equal(PlatformAdminTargetType, entry.TargetType);
            Assert.Null(entry.TargetId);
            Assert.Equal(PlatformAuditOutcome.Failed, entry.Outcome);
            Assert.Equal("invalid_refresh_token", entry.Reason);
            Assert.Equal(1, unitOfWork.BeginCount);
            Assert.Equal(1, unitOfWork.CommitCount);
            Assert.Equal(new[] { FailedAuditAction, HandlerCallLog.BeginStep, HandlerCallLog.CommitStep }, callLog.Steps);
            Assert.Empty(adminAccountReader.RequestedAdminIds);
            Assert.Empty(accessTokenIssuer.Requests);
            Assert.Empty(refreshTokenService.Revocations);
        }

        [Fact]
        public async Task AnInactiveAdmin_RevokesTheRotatedFamilyWithTheAdminRevokedReason_AndReturnsTheSafeError()
        {
            var adminId = Guid.NewGuid();
            SetSuccessfulRotation(adminId);
            adminAccountReader.NextAdmin = null;

            var result = await HandleRefreshAsync();

            Assert.True(result.IsFailure);
            Assert.Equal(AuthErrors.InvalidRefreshTokenCode, result.Error!.Code);
            Assert.Equal(AuthErrors.InvalidRefreshTokenMessage, result.Error.Message);
            Assert.Equal(ErrorType.Unauthorized, result.Error.Type);
            var revocation = Assert.Single(refreshTokenService.Revocations);
            Assert.Equal(RotatedRefreshTokenValue, revocation.RawToken);
            Assert.Equal("admin_revoked", revocation.Reason);
            var entry = Assert.Single(auditWriter.Entries);
            Assert.Equal(FailedAuditAction, entry.Action);
            Assert.Equal(PlatformAdminTargetType, entry.TargetType);
            Assert.Equal(adminId, entry.TargetId);
            Assert.Equal(PlatformAuditOutcome.Failed, entry.Outcome);
            Assert.Equal("admin_inactive", entry.Reason);
            Assert.Empty(accessTokenIssuer.Requests);
        }

        [Fact]
        public async Task RefreshFailures_NeverPutTheSubmittedTokenInTheResponseOrTheAuditEntry()
        {
            refreshTokenService.NextRotation = Result.Failure<RotatedRefreshToken>(AuthErrors.InvalidRefreshToken());

            var result = await HandleRefreshAsync();

            Assert.True(result.IsFailure);
            Assert.DoesNotContain(SubmittedRefreshToken, result.Error!.Message, StringComparison.Ordinal);
            var entry = Assert.Single(auditWriter.Entries);
            Assert.DoesNotContain(SubmittedRefreshToken, entry.Action, StringComparison.Ordinal);
            Assert.DoesNotContain(SubmittedRefreshToken, entry.Reason!, StringComparison.Ordinal);
        }
    }
}
