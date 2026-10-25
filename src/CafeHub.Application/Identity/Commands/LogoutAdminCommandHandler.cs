using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Domain.Identity;

namespace CafeHub.Application.Identity.Commands
{
    public sealed class LogoutAdminCommandHandler : ICommandHandler<LogoutAdminCommand>
    {
        private const string SucceededAction = "admin.logout.succeeded";
        private const string FailedAction = "admin.logout.failed";
        private const string PlatformAdminSubjectType = "platform_admin";
        private const string InvalidRefreshTokenReason = "invalid_refresh_token";

        private readonly IRefreshTokenService refreshTokenService;
        private readonly IPlatformAuditWriter auditWriter;
        private readonly IMasterUnitOfWork masterUnitOfWork;

        public LogoutAdminCommandHandler(
            IRefreshTokenService refreshTokenService,
            IPlatformAuditWriter auditWriter,
            IMasterUnitOfWork masterUnitOfWork)
        {
            this.refreshTokenService = refreshTokenService ?? throw new ArgumentNullException(nameof(refreshTokenService));
            this.auditWriter = auditWriter ?? throw new ArgumentNullException(nameof(auditWriter));
            this.masterUnitOfWork = masterUnitOfWork ?? throw new ArgumentNullException(nameof(masterUnitOfWork));
        }

        public async Task<Result> Handle(LogoutAdminCommand command, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var revocation = await refreshTokenService.RevokeFamilyAsync(command.RefreshToken, RefreshTokenRevocationReasons.Logout, cancellationToken);

            if (revocation.IsFailure)
            {
                auditWriter.Write(new PlatformAuditEntry
                {
                    ActorKind = PlatformAuditActorKind.System,
                    Action = FailedAction,
                    TargetType = PlatformAdminSubjectType,
                    Outcome = PlatformAuditOutcome.Failed,
                    Reason = InvalidRefreshTokenReason,
                });
                await FlushAuditEntriesAsync(cancellationToken);

                return Result.Failure(revocation.Error!);
            }

            auditWriter.Write(new PlatformAuditEntry
            {
                ActorKind = PlatformAuditActorKind.System,
                Action = SucceededAction,
                TargetType = PlatformAdminSubjectType,
                TargetId = revocation.Value,
                Outcome = PlatformAuditOutcome.Succeeded,
            });
            await FlushAuditEntriesAsync(cancellationToken);

            return Result.Success();
        }

        private async Task FlushAuditEntriesAsync(CancellationToken cancellationToken)
        {
            await masterUnitOfWork.BeginTransactionAsync(cancellationToken);
            await masterUnitOfWork.CommitTransactionAsync(cancellationToken);
        }
    }
}
