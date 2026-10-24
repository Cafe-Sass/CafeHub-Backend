using System.Security.Claims;
using System.Text;
using CafeHub.Application.Identity;
using CafeHub.Infrastructure.Authentication;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CafeHub.UnitTests
{
    public sealed class JwtAccessTokenIssuerTests
    {
        private static readonly DateTimeOffset FixedNow = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

        private const string Issuer = "https://issuer.cafehub.test";
        private const string Audience = "cafehub-api";

        private static JwtOptions CreateValidOptions()
        {
            return new JwtOptions
            {
                Issuer = Issuer,
                Audience = Audience,
                SigningKey = "0123456789abcdef0123456789abcdef",
                AccessTokenLifetimeMinutes = 15,
                ClockSkewSeconds = 30,
            };
        }

        private static AccessTokenRequest CreateRequest()
        {
            return new AccessTokenRequest
            {
                AdministratorId = new Guid("0b6f4c56-2b45-4a7e-9de0-5b8c1d2e3f40"),
                Email = "admin@cafehub.test",
                Roles = new[] { "SuperAdmin", "Operations" },
            };
        }

        private static JwtAccessTokenIssuer CreateIssuer(JwtOptions? options = null, TimeProvider? timeProvider = null)
        {
            return new JwtAccessTokenIssuer(options ?? CreateValidOptions(), timeProvider ?? new FixedTimeProvider(FixedNow));
        }

        private static JsonWebToken ReadToken(string token)
        {
            return new JsonWebTokenHandler().ReadJsonWebToken(token);
        }

        [Fact]
        public void Issue_TokenCarriesSubEmailRolesAndJti()
        {
            var request = CreateRequest();

            var accessToken = CreateIssuer().Issue(request);

            var jsonWebToken = ReadToken(accessToken.Value);
            Assert.Equal(request.AdministratorId.ToString(), jsonWebToken.GetClaim("sub").Value);
            Assert.Equal(request.Email, jsonWebToken.GetClaim("email").Value);
            Assert.True(Guid.TryParse(jsonWebToken.GetClaim("jti").Value, out _));

            var roleValues = jsonWebToken.Claims.Where(claim => claim.Type == "role").Select(claim => claim.Value).ToList();
            Assert.Equal(2, roleValues.Count);
            Assert.Contains("SuperAdmin", roleValues);
            Assert.Contains("Operations", roleValues);
        }

        [Fact]
        public void Issue_TokenExpiresAtIssueTimePlusConfiguredLifetime()
        {
            var accessToken = CreateIssuer().Issue(CreateRequest());

            var jsonWebToken = ReadToken(accessToken.Value);
            Assert.Equal(FixedNow.UtcDateTime, jsonWebToken.IssuedAt);
            Assert.Equal(FixedNow.AddMinutes(15).UtcDateTime, jsonWebToken.ValidTo);
            Assert.Equal(FixedNow.AddMinutes(15), accessToken.ExpiresAt);
        }

        [Fact]
        public void Issue_TwoTokens_HaveDifferentJti()
        {
            var issuer = CreateIssuer();

            var firstToken = ReadToken(issuer.Issue(CreateRequest()).Value);
            var secondToken = ReadToken(issuer.Issue(CreateRequest()).Value);

            Assert.NotEqual(firstToken.GetClaim("jti").Value, secondToken.GetClaim("jti").Value);
        }

        [Fact]
        public void Issue_TokenIsSignedWithHs256()
        {
            var accessToken = CreateIssuer().Issue(CreateRequest());

            Assert.Equal("HS256", ReadToken(accessToken.Value).Alg);
        }

        [Fact]
        public async Task Issue_TokenValidatesWithTheConfiguredKey()
        {
            var options = CreateValidOptions();
            var accessToken = CreateIssuer(options, TimeProvider.System).Issue(CreateRequest());

            var result = await new JsonWebTokenHandler().ValidateTokenAsync(accessToken.Value, CreateValidationParameters(options.SigningKey));

            Assert.True(result.IsValid);
            var identity = result.ClaimsIdentity;
            Assert.NotNull(identity);
            Assert.Equal(Issuer, identity.FindFirst("iss")!.Value);
            Assert.Equal(Audience, identity.FindFirst("aud")!.Value);
        }

        [Fact]
        public async Task Issue_TokenFailsValidation_WithADifferentKey()
        {
            var options = CreateValidOptions();
            var accessToken = CreateIssuer(options, TimeProvider.System).Issue(CreateRequest());

            var result = await new JsonWebTokenHandler().ValidateTokenAsync(accessToken.Value, CreateValidationParameters("ffffffffffffffffffffffffffffffff"));

            Assert.False(result.IsValid);
            Assert.IsType<SecurityTokenSignatureKeyNotFoundException>(result.Exception);
        }

        [Fact]
        public async Task Issue_ExpiredTokenFailsValidation_BeyondTheConfiguredClockSkew()
        {
            var options = CreateValidOptions();
            options.ClockSkewSeconds = 30;
            var issuedOneHourAgo = TimeProvider.System.GetUtcNow().AddHours(-1);
            var accessToken = CreateIssuer(options, new FixedTimeProvider(issuedOneHourAgo)).Issue(CreateRequest());

            var result = await new JsonWebTokenHandler().ValidateTokenAsync(accessToken.Value, CreateValidationParameters(options.SigningKey));

            Assert.False(result.IsValid);
            Assert.IsType<SecurityTokenExpiredException>(result.Exception);
        }

        private static TokenValidationParameters CreateValidationParameters(string signingKey)
        {
            return new TokenValidationParameters
            {
                ValidIssuer = Issuer,
                ValidAudience = Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                RequireExpirationTime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
            };
        }

        private sealed class FixedTimeProvider : TimeProvider
        {
            private readonly DateTimeOffset utcNow;

            public FixedTimeProvider(DateTimeOffset utcNow)
            {
                this.utcNow = utcNow;
            }

            public override DateTimeOffset GetUtcNow() => utcNow;
        }
    }
}
