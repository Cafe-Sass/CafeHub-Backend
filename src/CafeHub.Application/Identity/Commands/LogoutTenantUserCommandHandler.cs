using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Domain.Identity;

namespace CafeHub.Application.Identity.Commands
{
    public sealed class LogoutTenantUserCommandHandler : ICommandHandler<LogoutTenantUserCommand>
    {
        private readonly ITenantRefreshTokenService refreshTokenService;
        private readonly ITenantAuditWriter auditWriter;
        private readonly ITenantUnitOfWork tenantUnitOfWork;

        public LogoutTenantUserCommandHandler(
            ITenantRefreshTokenService refreshTokenService,
            ITenantAuditWriter auditWriter,
            ITenantUnitOfWork tenantUnitOfWork)
        {
            this.refreshTokenService = refreshTokenService ?? throw new ArgumentNullException(nameof(refreshTokenService));
            this.auditWriter = auditWriter ?? throw new ArgumentNullException(nameof(auditWriter));
            this.tenantUnitOfWork = tenantUnitOfWork ?? throw new ArgumentNullException(nameof(tenantUnitOfWork));
        }

        public async Task<Result> Handle(LogoutTenantUserCommand command, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);
            await refreshTokenService.RevokeFamilyAsync(command.RefreshToken, RefreshTokenRevocationReasons.Logout, cancellationToken);
            await TenantAuthenticationAudit.WriteAndCommitAsync(auditWriter, tenantUnitOfWork, new TenantAuditEntry
            {
                Action = "tenant.logout.succeeded",
                TargetType = "refresh_token",
            }, cancellationToken);
            return Result.Success();
        }
    }
}
