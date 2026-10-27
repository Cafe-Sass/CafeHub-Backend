using System.Text;
using CafeHub.Infrastructure.Authentication;
using Microsoft.IdentityModel.Tokens;

namespace CafeHub.WebApi.Authentication
{
    internal static class TenantTokenValidationParameters
    {
        public static TokenValidationParameters Create(JwtOptions jwtOptions, TenantJwtOptions tenantJwtOptions)
        {
            ArgumentNullException.ThrowIfNull(jwtOptions);
            ArgumentNullException.ThrowIfNull(tenantJwtOptions);

            return new TokenValidationParameters
            {
                ValidIssuer = jwtOptions.Issuer,
                ValidAudience = tenantJwtOptions.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                RequireExpirationTime = true,
                ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
                ClockSkew = TimeSpan.FromSeconds(jwtOptions.ClockSkewSeconds),
                NameClaimType = PlatformTokenValidationParameters.NameClaimType,
                RoleClaimType = PlatformTokenValidationParameters.RoleClaimType,
            };
        }
    }
}
