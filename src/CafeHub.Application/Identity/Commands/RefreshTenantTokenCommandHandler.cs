using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity.DTOs;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Identity;

namespace CafeHub.Application.Identity.Commands
{
    public sealed class RefreshTenantTokenCommandHandler : ICommandHandler<RefreshTenantTokenCommand, TenantTokenResponse>
    {
        private readonly ITenantRefreshTokenService refreshTokenService;
        private readonly ITenantUserAuthenticator userAuthenticator;
        private readonly ITenantAccessTokenIssuer accessTokenIssuer;
        private readonly ITenantContextAccessor tenantContextAccessor;

        public RefreshTenantTokenCommandHandler(ITenantRefreshTokenService refreshTokenService, ITenantUserAuthenticator userAuthenticator, ITenantAccessTokenIssuer accessTokenIssuer, ITenantContextAccessor tenantContextAccessor)
        {
            this.refreshTokenService = refreshTokenService ?? throw new ArgumentNullException(nameof(refreshTokenService));
            this.userAuthenticator = userAuthenticator ?? throw new ArgumentNullException(nameof(userAuthenticator));
            this.accessTokenIssuer = accessTokenIssuer ?? throw new ArgumentNullException(nameof(accessTokenIssuer));
            this.tenantContextAccessor = tenantContextAccessor ?? throw new ArgumentNullException(nameof(tenantContextAccessor));
        }

        public async Task<Result<TenantTokenResponse>> Handle(RefreshTenantTokenCommand command, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);
            var rotation = await refreshTokenService.RotateAsync(command.RefreshToken, cancellationToken);
            if (rotation.IsFailure) return Result.Failure<TenantTokenResponse>(AuthErrors.InvalidRefreshToken());

            var rotated = rotation.Value;
            var user = await userAuthenticator.FindByIdAsync(rotated.UserId, cancellationToken);
            if (user is null)
            {
                await refreshTokenService.RevokeFamilyAsync(rotated.RawToken, RefreshTokenRevocationReasons.AdminRevoked, cancellationToken);
                return Result.Failure<TenantTokenResponse>(AuthErrors.InvalidRefreshToken());
            }

            var accessToken = accessTokenIssuer.Issue(new TenantAccessTokenRequest
            {
                UserId = user.Id,
                Email = user.Email,
                Roles = user.Roles,
                TenantId = tenantContextAccessor.GetRequired().TenantId,
            });

            return Result.Success(new TenantTokenResponse
            {
                AccessToken = accessToken.Value,
                AccessTokenExpiresAt = accessToken.ExpiresAt,
                RefreshToken = rotated.RawToken,
                RefreshTokenExpiresAt = rotated.ExpiresAt,
            });
        }
    }
}
