using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Identity.DTOs;
using CafeHub.Application.Tenancy;

namespace CafeHub.Application.Identity.Commands
{
    public sealed class LoginTenantUserCommandHandler : ICommandHandler<LoginTenantUserCommand, TenantTokenResponse>
    {
        private readonly ITenantUserAuthenticator authenticator;
        private readonly ITenantAccessTokenIssuer accessTokenIssuer;
        private readonly ITenantRefreshTokenService refreshTokenService;
        private readonly ITenantContextAccessor tenantContextAccessor;
        private readonly ITenantAuditWriter auditWriter;
        private readonly ITenantUnitOfWork tenantUnitOfWork;

        public LoginTenantUserCommandHandler(
            ITenantUserAuthenticator authenticator,
            ITenantAccessTokenIssuer accessTokenIssuer,
            ITenantRefreshTokenService refreshTokenService,
            ITenantContextAccessor tenantContextAccessor,
            ITenantAuditWriter auditWriter,
            ITenantUnitOfWork tenantUnitOfWork)
        {
            this.authenticator = authenticator ?? throw new ArgumentNullException(nameof(authenticator));
            this.accessTokenIssuer = accessTokenIssuer ?? throw new ArgumentNullException(nameof(accessTokenIssuer));
            this.refreshTokenService = refreshTokenService ?? throw new ArgumentNullException(nameof(refreshTokenService));
            this.tenantContextAccessor = tenantContextAccessor ?? throw new ArgumentNullException(nameof(tenantContextAccessor));
            this.auditWriter = auditWriter ?? throw new ArgumentNullException(nameof(auditWriter));
            this.tenantUnitOfWork = tenantUnitOfWork ?? throw new ArgumentNullException(nameof(tenantUnitOfWork));
        }

        public async Task<Result<TenantTokenResponse>> Handle(LoginTenantUserCommand command, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);
            var authentication = await authenticator.CheckCredentialsAsync(command.Email, command.Password, cancellationToken);
            if (authentication.IsFailure)
            {
                var rejection = authentication.Error!;
                var userId = TenantUserIdOf(rejection);
                await TenantAuthenticationAudit.WriteAndCommitAsync(auditWriter, tenantUnitOfWork, new TenantAuditEntry
                {
                    Action = "tenant.login.failed",
                    TargetType = "tenant_user",
                    TargetId = userId,
                    ActorUserId = userId,
                    Reason = rejection is LockedOutTenantCredentials ? TenantAuditReason.LockedOut : TenantAuditReason.InvalidCredentials,
                }, cancellationToken);
                return Result.Failure<TenantTokenResponse>(rejection);
            }

            var tenantId = tenantContextAccessor.GetRequired().TenantId;
            var user = authentication.Value;
            var accessToken = accessTokenIssuer.Issue(new TenantAccessTokenRequest { UserId = user.Id, Email = user.Email, Roles = user.Roles, TenantId = tenantId });
            var refreshToken = await refreshTokenService.IssueAsync(user.Id, cancellationToken);
            if (refreshToken.IsFailure)
            {
                await TenantAuthenticationAudit.WriteAndCommitAsync(auditWriter, tenantUnitOfWork, new TenantAuditEntry
                {
                    Action = "tenant.login.failed",
                    TargetType = "tenant_user",
                    TargetId = user.Id,
                    ActorUserId = user.Id,
                }, cancellationToken);
                return Result.Failure<TenantTokenResponse>(refreshToken.Error!);
            }

            await TenantAuthenticationAudit.WriteAndCommitAsync(auditWriter, tenantUnitOfWork, new TenantAuditEntry
            {
                Action = "tenant.login.succeeded",
                TargetType = "tenant_user",
                TargetId = user.Id,
                ActorUserId = user.Id,
            }, cancellationToken);

            return Result.Success(new TenantTokenResponse
            {
                AccessToken = accessToken.Value,
                AccessTokenExpiresAt = accessToken.ExpiresAt,
                RefreshToken = refreshToken.Value.RawToken,
                RefreshTokenExpiresAt = refreshToken.Value.ExpiresAt,
            });
        }

        private static Guid? TenantUserIdOf(Error error)
        {
            return error switch
            {
                RejectedTenantCredentials rejected => rejected.UserId,
                LockedOutTenantCredentials lockedOut => lockedOut.UserId,
                _ => null,
            };
        }
    }
}
