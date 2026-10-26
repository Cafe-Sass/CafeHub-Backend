using System.Security.Claims;
using System.Text;
using CafeHub.Application.Identity;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CafeHub.Infrastructure.Authentication
{
    public sealed class TenantJwtAccessTokenIssuer : ITenantAccessTokenIssuer
    {
        private readonly JwtOptions commonOptions;
        private readonly TenantJwtOptions tenantOptions;
        private readonly TimeProvider timeProvider;
        private readonly SigningCredentials signingCredentials;
        private readonly JsonWebTokenHandler handler = new();

        public TenantJwtAccessTokenIssuer(JwtOptions commonOptions, TenantJwtOptions tenantOptions, TimeProvider timeProvider)
        {
            this.commonOptions = commonOptions ?? throw new ArgumentNullException(nameof(commonOptions));
            this.tenantOptions = tenantOptions ?? throw new ArgumentNullException(nameof(tenantOptions));
            this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            signingCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(this.commonOptions.SigningKey)), SecurityAlgorithms.HmacSha256);
        }

        public AccessToken Issue(TenantAccessTokenRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            var now = timeProvider.GetUtcNow();
            var expires = now.AddMinutes(tenantOptions.AccessTokenLifetimeMinutes);
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, request.UserId.ToString()),
                new(JwtRegisteredClaimNames.Email, request.Email),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new("tid", request.TenantId.ToString()),
                new("token_kind", "tenant"),
            };
            claims.AddRange(request.Roles.Select(role => new Claim("role", role)));
            var token = handler.CreateToken(new SecurityTokenDescriptor
            {
                Issuer = commonOptions.Issuer,
                Audience = tenantOptions.Audience,
                IssuedAt = now.UtcDateTime,
                NotBefore = now.UtcDateTime,
                Expires = expires.UtcDateTime,
                SigningCredentials = signingCredentials,
                Subject = new ClaimsIdentity(claims),
            });
            return new AccessToken { Value = token, ExpiresAt = expires };
        }
    }
}
