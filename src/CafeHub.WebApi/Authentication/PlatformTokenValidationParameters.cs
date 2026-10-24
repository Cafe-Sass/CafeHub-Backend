using System.Text;
using CafeHub.Infrastructure.Authentication;
using Microsoft.IdentityModel.Tokens;

namespace CafeHub.WebApi.Authentication
{
    internal static class PlatformTokenValidationParameters
    {
        public const string NameClaimType = "sub";

        public const string RoleClaimType = "role";

        public static TokenValidationParameters Create(JwtOptions jwtOptions)
        {
            ArgumentNullException.ThrowIfNull(jwtOptions);

            return new TokenValidationParameters
            {
                ValidIssuer = jwtOptions.Issuer,
                ValidAudience = jwtOptions.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                RequireExpirationTime = true,
                ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
                ClockSkew = TimeSpan.FromSeconds(jwtOptions.ClockSkewSeconds),
                NameClaimType = NameClaimType,
                RoleClaimType = RoleClaimType,
            };
        }
    }
}
