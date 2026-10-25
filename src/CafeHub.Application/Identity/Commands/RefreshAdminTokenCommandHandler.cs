using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Identity.DTOs;
using CafeHub.Domain.Identity;

namespace CafeHub.Application.Identity.Commands
{
    public sealed class RefreshAdminTokenCommandHandler : ICommandHandler<RefreshAdminTokenCommand, AdminTokenResponse>
    {
        private const string SucceededAction = "admin.refresh.succeeded";
        private const string FailedAction = "admin.refresh.failed";
        private const string PlatformAdminSubjectType = "platform_admin";
        private const string InvalidRefreshTokenReason = "invalid_refresh_token";
        private const string AdminInactiveReason = "admin_inactive";

        private readonly IRefreshTokenService refreshTokenService;
        private readonly IAdminAccountReader adminAccountReader;
        private readonly IAccessTokenIssuer accessTokenIssuer;
        private readonly IPlatformAuditWriter auditWriter;
        private readonly IMasterUnitOfWork masterUnitOfWork;

        public RefreshAdminTokenCommandHandler(
            IRefreshTokenService refreshTokenService,
            IAdminAccountReader adminAccountReader,
            IAccessTokenIssuer accessTokenIssuer,
            IPlatformAuditWriter auditWriter,
            IMasterUnitOfWork masterUnitOfWork)
        {
            this.refreshTokenService = refreshTokenService ?? throw new ArgumentNullException(nameof(refreshTokenService));
            this.adminAccountReader = adminAccountReader ?? throw new ArgumentNullException(nameof(adminAccountReader));
            this.accessTokenIssuer = accessTokenIssuer ?? throw new ArgumentNullException(nameof(accessTokenIssuer));
            this.auditWriter = auditWriter ?? throw new ArgumentNullException(nameof(auditWriter));
            this.masterUnitOfWork = masterUnitOfWork ?? throw new ArgumentNullException(nameof(masterUnitOfWork));
        }

        public async Task<Result<AdminTokenResponse>> Handle(RefreshAdminTokenCommand command, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var rotation = await refreshTokenService.RotateAsync(command.RefreshToken, cancellationToken);

            if (rotation.IsFailure)
            {
                await WriteFailureAsync(null, InvalidRefreshTokenReason, cancellationToken);
                return Result.Failure<AdminTokenResponse>(rotation.Error!);
            }

            var rotatedToken = rotation.Value;
            var admin = await adminAccountReader.FindActiveByIdAsync(rotatedToken.AdminId, cancellationToken);

            if (admin is null)
            {
                await refreshTokenService.RevokeFamilyAsync(rotatedToken.RawToken, RefreshTokenRevocationReasons.AdminRevoked, cancellationToken);
                await WriteFailureAsync(rotatedToken.AdminId, AdminInactiveReason, cancellationToken);
                return Result.Failure<AdminTokenResponse>(AuthErrors.InvalidRefreshToken());
            }

            var accessToken = accessTokenIssuer.Issue(new AccessTokenRequest
            {
                AdministratorId = admin.Id,
                Email = admin.Email,
                Roles = admin.Roles,
            });

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
                RefreshToken = rotatedToken.RawToken,
                RefreshTokenExpiresAt = rotatedToken.ExpiresAt,
            });
        }

        private async Task WriteFailureAsync(Guid? administratorId, string reason, CancellationToken cancellationToken)
        {
            auditWriter.Write(new PlatformAuditEntry
            {
                ActorKind = PlatformAuditActorKind.System,
                Action = FailedAction,
                TargetType = PlatformAdminSubjectType,
                TargetId = administratorId,
                Outcome = PlatformAuditOutcome.Failed,
                Reason = reason,
            });
            await FlushAuditEntriesAsync(cancellationToken);
        }

        private async Task FlushAuditEntriesAsync(CancellationToken cancellationToken)
        {
            await masterUnitOfWork.BeginTransactionAsync(cancellationToken);
            await masterUnitOfWork.CommitTransactionAsync(cancellationToken);
        }
    }
}
