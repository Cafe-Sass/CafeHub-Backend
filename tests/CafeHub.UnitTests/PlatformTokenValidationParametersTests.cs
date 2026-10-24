using System.Security.Claims;
using CafeHub.Application.Identity;
using CafeHub.Infrastructure.Authentication;
using CafeHub.WebApi.Authentication;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CafeHub.UnitTests
{
    public sealed class PlatformTokenValidationParametersTests
    {
        private const string Issuer = "https://issuer.cafehub.test";
        private const string Audience = "cafehub-api";

        private static JwtOptions CreateOptions()
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
                Roles = new[] { "SuperAdmin" },
            };
        }

        [Fact]
        public void Create_RestrictsValidAlgorithmsToHs256()
        {
            var parameters = PlatformTokenValidationParameters.Create(CreateOptions());

            var algorithm = Assert.Single(parameters.ValidAlgorithms!);
            Assert.Equal(SecurityAlgorithms.HmacSha256, algorithm);
        }

        [Fact]
        public void Create_EnablesEveryRequiredValidation()
        {
            var parameters = PlatformTokenValidationParameters.Create(CreateOptions());

            Assert.True(parameters.ValidateIssuer);
            Assert.True(parameters.ValidateAudience);
            Assert.True(parameters.ValidateLifetime);
            Assert.True(parameters.ValidateIssuerSigningKey);
            Assert.True(parameters.RequireExpirationTime);
        }

        [Fact]
        public void Create_ClockSkewEqualsTheConfiguredOption()
        {
            var options = CreateOptions();
            options.ClockSkewSeconds = 45;

            var parameters = PlatformTokenValidationParameters.Create(options);

            Assert.Equal(TimeSpan.FromSeconds(45), parameters.ClockSkew);
        }

        [Fact]
        public void Create_SetsTheWireClaimNamesForNameAndRole()
        {
            var parameters = PlatformTokenValidationParameters.Create(CreateOptions());

            Assert.Equal("sub", parameters.NameClaimType);
            Assert.Equal("role", parameters.RoleClaimType);
        }

        [Fact]
        public async Task Create_ParametersExposeTheWireRoleClaim_OnTheAuthenticatedPrincipal()
        {
            var options = CreateOptions();
            var accessToken = new JwtAccessTokenIssuer(options, TimeProvider.System).Issue(CreateRequest());

            var result = await new JsonWebTokenHandler().ValidateTokenAsync(accessToken.Value, PlatformTokenValidationParameters.Create(options));

            Assert.True(result.IsValid);
            var identity = result.ClaimsIdentity;
            Assert.NotNull(identity);
            Assert.Equal("role", identity.RoleClaimType);
            Assert.True(new ClaimsPrincipal(identity).IsInRole("SuperAdmin"));
        }

        [Fact]
        public async Task Create_ParametersValidateATokenIssuedWithTheSameOptions()
        {
            var options = CreateOptions();
            var accessToken = new JwtAccessTokenIssuer(options, TimeProvider.System).Issue(CreateRequest());

            var result = await new JsonWebTokenHandler().ValidateTokenAsync(accessToken.Value, PlatformTokenValidationParameters.Create(options));

            Assert.True(result.IsValid);
        }

        [Fact]
        public async Task Create_ParametersRejectATokenSignedWithADifferentKey()
        {
            var issuedWith = CreateOptions();
            var accessToken = new JwtAccessTokenIssuer(issuedWith, TimeProvider.System).Issue(CreateRequest());
            var validatedWith = CreateOptions();
            validatedWith.SigningKey = "ffffffffffffffffffffffffffffffff";

            var result = await new JsonWebTokenHandler().ValidateTokenAsync(accessToken.Value, PlatformTokenValidationParameters.Create(validatedWith));

            Assert.False(result.IsValid);
            Assert.IsType<SecurityTokenSignatureKeyNotFoundException>(result.Exception);
        }

        [Fact]
        public async Task Create_ParametersRejectATokenWithAWrongIssuer()
        {
            var issuedWith = CreateOptions();
            issuedWith.Issuer = "https://other-issuer.cafehub.test";
            var accessToken = new JwtAccessTokenIssuer(issuedWith, TimeProvider.System).Issue(CreateRequest());

            var result = await new JsonWebTokenHandler().ValidateTokenAsync(accessToken.Value, PlatformTokenValidationParameters.Create(CreateOptions()));

            Assert.False(result.IsValid);
            Assert.IsType<SecurityTokenInvalidIssuerException>(result.Exception);
        }

        [Fact]
        public async Task Create_ParametersRejectATokenWithAWrongAudience()
        {
            var issuedWith = CreateOptions();
            issuedWith.Audience = "other-api";
            var accessToken = new JwtAccessTokenIssuer(issuedWith, TimeProvider.System).Issue(CreateRequest());

            var result = await new JsonWebTokenHandler().ValidateTokenAsync(accessToken.Value, PlatformTokenValidationParameters.Create(CreateOptions()));

            Assert.False(result.IsValid);
            Assert.IsType<SecurityTokenInvalidAudienceException>(result.Exception);
        }

        [Fact]
        public async Task Create_ParametersRejectAnUnsignedNoneAlgorithmToken()
        {
            var options = CreateOptions();
            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = Issuer,
                Audience = Audience,
                IssuedAt = DateTime.UtcNow,
                NotBefore = DateTime.UtcNow,
                Expires = DateTime.UtcNow.AddMinutes(15),
            };
            var unsignedToken = new JsonWebTokenHandler().CreateToken(descriptor);

            var result = await new JsonWebTokenHandler().ValidateTokenAsync(unsignedToken, PlatformTokenValidationParameters.Create(options));

            Assert.False(result.IsValid);
            Assert.NotNull(result.Exception);
        }

        [Fact]
        public async Task Create_ParametersRejectATokenExpiredBeyondTheClockSkew()
        {
            var options = CreateOptions();
            var issuedOneHourAgo = TimeProvider.System.GetUtcNow().AddHours(-1);
            var accessToken = new JwtAccessTokenIssuer(options, new FixedTimeProvider(issuedOneHourAgo)).Issue(CreateRequest());

            var result = await new JsonWebTokenHandler().ValidateTokenAsync(accessToken.Value, PlatformTokenValidationParameters.Create(options));

            Assert.False(result.IsValid);
            Assert.IsType<SecurityTokenExpiredException>(result.Exception);
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
