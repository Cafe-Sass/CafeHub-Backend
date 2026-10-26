using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
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

        public LoginTenantUserCommandHandler(ITenantUserAuthenticator authenticator, ITenantAccessTokenIssuer accessTokenIssuer, ITenantRefreshTokenService refreshTokenService, ITenantContextAccessor tenantContextAccessor)
        {
            this.authenticator = authenticator;
            this.accessTokenIssuer = accessTokenIssuer;
            this.refreshTokenService = refreshTokenService;
            this.tenantContextAccessor = tenantContextAccessor;
        }

        public async Task<Result<TenantTokenResponse>> Handle(LoginTenantUserCommand command, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);
            var authentication = await authenticator.CheckCredentialsAsync(command.Email, command.Password, cancellationToken);
            if (authentication.IsFailure) return Result.Failure<TenantTokenResponse>(authentication.Error!);

            var tenantId = tenantContextAccessor.GetRequired().TenantId;
            var user = authentication.Value;
            var accessToken = accessTokenIssuer.Issue(new TenantAccessTokenRequest { UserId = user.Id, Email = user.Email, Roles = user.Roles, TenantId = tenantId });
            var refreshToken = await refreshTokenService.IssueAsync(user.Id, cancellationToken);
            if (refreshToken.IsFailure) return Result.Failure<TenantTokenResponse>(refreshToken.Error!);

            return Result.Success(new TenantTokenResponse
            {
                AccessToken = accessToken.Value,
                AccessTokenExpiresAt = accessToken.ExpiresAt,
                RefreshToken = refreshToken.Value.RawToken,
                RefreshTokenExpiresAt = refreshToken.Value.ExpiresAt,
            });
        }
    }
}
