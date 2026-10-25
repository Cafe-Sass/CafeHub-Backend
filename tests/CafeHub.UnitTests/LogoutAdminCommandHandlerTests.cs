using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Identity;
using CafeHub.Application.Identity.Commands;

namespace CafeHub.UnitTests
{
    public sealed class LogoutAdminCommandHandlerTests
    {
        private const string SubmittedRefreshToken = "submitted-refresh-token";
        private const string SucceededAuditAction = "admin.logout.succeeded";
        private const string FailedAuditAction = "admin.logout.failed";
        private const string PlatformAdminTargetType = "platform_admin";

        private readonly HandlerCallLog callLog = new();
        private readonly SessionRefreshTokenServiceStub refreshTokenService;
        private readonly RecordingAuditWriter auditWriter;
        private readonly RecordingAuditFlushUnitOfWork unitOfWork;
        private readonly LogoutAdminCommandHandler handler;

        public LogoutAdminCommandHandlerTests()
        {
            refreshTokenService = new SessionRefreshTokenServiceStub();
            auditWriter = new RecordingAuditWriter(callLog);
            unitOfWork = new RecordingAuditFlushUnitOfWork(callLog);
            handler = new LogoutAdminCommandHandler(refreshTokenService, auditWriter, unitOfWork);
        }

        private Task<Result> HandleLogoutAsync()
        {
            return handler.Handle(new LogoutAdminCommand(SubmittedRefreshToken), CancellationToken.None);
        }

        [Fact]
        public async Task TheRefreshToken_IsForwardedToTheRevocationService_WithTheLogoutReason()
        {
            refreshTokenService.NextRevocation = Result.Success(Guid.NewGuid());

            await HandleLogoutAsync();

            var revocation = Assert.Single(refreshTokenService.Revocations);
            Assert.Equal(SubmittedRefreshToken, revocation.RawToken);
            Assert.Equal("logout", revocation.Reason);
        }

        [Fact]
        public async Task AValidRefreshToken_RevokesTheFamily_AndWritesASucceededAuditEntry_WithTheAdminId()
        {
            var adminId = Guid.NewGuid();
            refreshTokenService.NextRevocation = Result.Success(adminId);

            var result = await HandleLogoutAsync();

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
        public async Task AnInvalidRefreshToken_ReturnsTheSafeError_AndWritesAFailedAuditEntry()
        {
            refreshTokenService.NextRevocation = Result.Failure<Guid>(AuthErrors.InvalidRefreshToken());

            var result = await HandleLogoutAsync();

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
        }

        [Fact]
        public async Task RepeatingLogoutWithTheSameToken_RemainsSuccessful_AndAuditsEachCall()
        {
            var adminId = Guid.NewGuid();
            refreshTokenService.NextRevocation = Result.Success(adminId);

            var first = await HandleLogoutAsync();
            var second = await HandleLogoutAsync();

            Assert.True(first.IsSuccess);
            Assert.True(second.IsSuccess);
            Assert.Equal(2, refreshTokenService.Revocations.Count);
            Assert.All(refreshTokenService.Revocations, call => Assert.Equal("logout", call.Reason));
            Assert.Equal(2, auditWriter.Entries.Count);
            Assert.All(auditWriter.Entries, entry => Assert.Equal(SucceededAuditAction, entry.Action));
            Assert.Equal(2, unitOfWork.BeginCount);
            Assert.Equal(2, unitOfWork.CommitCount);
        }

        [Fact]
        public async Task LogoutFailures_NeverPutTheSubmittedTokenInTheResponseOrTheAuditEntry()
        {
            refreshTokenService.NextRevocation = Result.Failure<Guid>(AuthErrors.InvalidRefreshToken());

            var result = await HandleLogoutAsync();

            Assert.True(result.IsFailure);
            Assert.DoesNotContain(SubmittedRefreshToken, result.Error!.Message, StringComparison.Ordinal);
            var entry = Assert.Single(auditWriter.Entries);
            Assert.DoesNotContain(SubmittedRefreshToken, entry.Action, StringComparison.Ordinal);
            Assert.DoesNotContain(SubmittedRefreshToken, entry.Reason!, StringComparison.Ordinal);
        }
    }
}
