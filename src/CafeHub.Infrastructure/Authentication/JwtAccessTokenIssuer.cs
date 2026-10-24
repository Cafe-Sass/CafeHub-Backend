using System.Security.Claims;
using System.Text;
using CafeHub.Application.Identity;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CafeHub.Infrastructure.Authentication
{
    public sealed class JwtAccessTokenIssuer : IAccessTokenIssuer
    {
        private const string RoleClaimType = "role";

        private readonly JwtOptions options;
        private readonly TimeProvider timeProvider;
        private readonly SigningCredentials signingCredentials;
        private readonly JsonWebTokenHandler tokenHandler = new();

        public JwtAccessTokenIssuer(JwtOptions options, TimeProvider timeProvider)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            signingCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(this.options.SigningKey)),
                SecurityAlgorithms.HmacSha256);
        }

        public AccessToken Issue(AccessTokenRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var issuedAt = timeProvider.GetUtcNow();
            var expiresAt = issuedAt.AddMinutes(options.AccessTokenLifetimeMinutes);

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, request.AdministratorId.ToString()),
                new(JwtRegisteredClaimNames.Email, request.Email),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };
            claims.AddRange(request.Roles.Select(role => new Claim(RoleClaimType, role)));

            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = options.Issuer,
                Audience = options.Audience,
                IssuedAt = issuedAt.UtcDateTime,
                NotBefore = issuedAt.UtcDateTime,
                Expires = expiresAt.UtcDateTime,
                SigningCredentials = signingCredentials,
                Subject = new ClaimsIdentity(claims, authenticationType: "JwtAccessTokenIssuer")
            };

            var token = tokenHandler.CreateToken(descriptor);

            return new AccessToken { Value = token, ExpiresAt = expiresAt };
        }
    }
}
