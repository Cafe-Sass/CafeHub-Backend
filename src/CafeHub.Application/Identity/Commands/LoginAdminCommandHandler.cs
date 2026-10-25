using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Identity;
using CafeHub.Application.Identity.DTOs;

namespace CafeHub.Application.Identity.Commands
{
    public sealed class LoginAdminCommandHandler : ICommandHandler<LoginAdminCommand, AdminTokenResponse>
    {
        private const string SucceededAction = "admin.login.succeeded";
        private const string FailedAction = "admin.login.failed";
        private const string PlatformAdminSubjectType = "platform_admin";
        private const string InvalidCredentialsReason = "invalid_credentials";
        private const string LockedOutReason = "locked_out";

        private readonly IAdminAuthenticator authenticator;
        private readonly IAccessTokenIssuer accessTokenIssuer;
        private readonly IRefreshTokenService refreshTokenService;
        private readonly IPlatformAuditWriter auditWriter;
        private readonly IMasterUnitOfWork masterUnitOfWork;

        public LoginAdminCommandHandler(
            IAdminAuthenticator authenticator,
            IAccessTokenIssuer accessTokenIssuer,
            IRefreshTokenService refreshTokenService,
            IPlatformAuditWriter auditWriter,
            IMasterUnitOfWork masterUnitOfWork)
        {
            this.authenticator = authenticator ?? throw new ArgumentNullException(nameof(authenticator));
            this.accessTokenIssuer = accessTokenIssuer ?? throw new ArgumentNullException(nameof(accessTokenIssuer));
            this.refreshTokenService = refreshTokenService ?? throw new ArgumentNullException(nameof(refreshTokenService));
            this.auditWriter = auditWriter ?? throw new ArgumentNullException(nameof(auditWriter));
            this.masterUnitOfWork = masterUnitOfWork ?? throw new ArgumentNullException(nameof(masterUnitOfWork));
        }

        public async Task<Result<AdminTokenResponse>> Handle(LoginAdminCommand command, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var authentication = await authenticator.CheckCredentialsAsync(command.Email, command.Password, cancellationToken);

            if (authentication.IsFailure)
            {
                var rejection = authentication.Error!;
                auditWriter.Write(new PlatformAuditEntry
                {
                    ActorKind = PlatformAuditActorKind.System,
                    Action = FailedAction,
                    TargetType = PlatformAdminSubjectType,
                    TargetId = AdministratorIdOf(rejection),
                    Outcome = PlatformAuditOutcome.Failed,
                    Reason = rejection is LockedOutAdminCredentials ? LockedOutReason : InvalidCredentialsReason,
                });
                await FlushAuditEntriesAsync(cancellationToken);

                return Result.Failure<AdminTokenResponse>(rejection);
            }

            var admin = authentication.Value;
            var accessToken = accessTokenIssuer.Issue(new AccessTokenRequest
            {
                AdministratorId = admin.Id,
                Email = admin.Email,
                Roles = admin.Roles,
            });

            var refreshToken = await refreshTokenService.IssueAsync(admin.Id, cancellationToken);
            if (refreshToken.IsFailure)
            {
                return Result.Failure<AdminTokenResponse>(refreshToken.Error!);
            }

            auditWriter.Write(new PlatformAuditEntry
            {
                ActorKind = PlatformAuditActorKind.System,
                Action = SucceededAction,
                TargetType = PlatformAdminSubjectType,
                TargetId = admin.Id,
                Outcome = PlatformAuditOutcome.Succeeded,
            });
            await FlushAuditEntriesAsync(cancellationToken);

            return Result.Success(new AdminTokenResponse
            {
                AccessToken = accessToken.Value,
                AccessTokenExpiresAt = accessToken.ExpiresAt,
                RefreshToken = refreshToken.Value.RawToken,
                RefreshTokenExpiresAt = refreshToken.Value.ExpiresAt,
            });
        }

        private static Guid? AdministratorIdOf(Error rejection)
        {
            return rejection switch
            {
                RejectedAdminCredentials rejected => rejected.AdministratorId,
                LockedOutAdminCredentials lockedOut => lockedOut.AdministratorId,
                _ => null,
            };
        }

        /// <summary>
        /// Audit entries are buffered in the shared master persistence context by <see cref="IPlatformAuditWriter"/>;
        /// this explicit unit-of-work round-trip persists them immediately, also for a failed login whose command
        /// result is a failure. The command is deliberately not marked master-transactional (the refresh token
        /// service opens its own master transactions and Identity persists lockout counters on its own), so no
        /// pipeline behavior rolls the buffered audit record back.
        /// </summary>
        private async Task FlushAuditEntriesAsync(CancellationToken cancellationToken)
        {
            await masterUnitOfWork.BeginTransactionAsync(cancellationToken);
            await masterUnitOfWork.CommitTransactionAsync(cancellationToken);
        }
    }
}
