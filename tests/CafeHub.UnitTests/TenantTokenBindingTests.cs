using System.Security.Claims;
using System.Text.Json;
using CafeHub.Application.Identity;
using CafeHub.Application.Tenancy;
using CafeHub.Infrastructure.Authentication;
using CafeHub.WebApi;
using CafeHub.WebApi.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CafeHub.UnitTests
{
    public sealed class TenantTokenBindingTests
    {
        private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
        private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid OtherTenantId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        private static JwtOptions PlatformOptions() => new()
        {
            Issuer = "https://issuer.cafehub.test",
            Audience = "platform-api",
            SigningKey = "0123456789abcdef0123456789abcdef",
            AccessTokenLifetimeMinutes = 15,
            ClockSkewSeconds = 30,
        };

        private static TenantJwtOptions TenantOptions() => new()
        {
            Audience = "tenant-api",
            AccessTokenLifetimeMinutes = 15,
        };

        private static (JwtBearerOptions Tenant, JwtBearerOptions Platform, ServiceProvider Services) RegisterSchemes(ITenantContextAccessor? accessor = null, ListLogger? logger = null)
        {
            var platform = PlatformOptions();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(platform);
            services.AddSingleton(TenantOptions());
            services.AddSingleton<ITenantContextAccessor>(accessor ?? new StubTenantContextAccessor(TenantContext.Create(TenantId, "tenant-secret-ref", true)));
            if (logger is not null) services.AddSingleton<ILogger<TenantJwtBearerEvents>>(logger);
            services.AddPlatformAuthentication();
            services.AddTenantAuthentication();
            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
            return (options.Get(TenantAuthenticationSchemes.Bearer), options.Get(JwtBearerDefaults.AuthenticationScheme), provider);
        }

        private static string CreateTenantToken(
            Guid? tenantId = null,
            string? tokenKind = "tenant",
            string issuer = "https://issuer.cafehub.test",
            string audience = "tenant-api",
            string signingKey = "0123456789abcdef0123456789abcdef",
            DateTimeOffset? issuedAt = null,
            bool includeTenantId = true,
            string? tenantIdValue = null,
            string algorithm = SecurityAlgorithms.HmacSha256)
        {
            var now = issuedAt ?? Now;
            var claims = new Dictionary<string, object>
            {
                ["sub"] = Guid.NewGuid().ToString(),
                ["email"] = "owner@example.com",
                ["jti"] = Guid.NewGuid().ToString(),
            };
            if (includeTenantId) claims["tid"] = tenantIdValue ?? (tenantId ?? TenantId).ToString();
            if (tokenKind is not null) claims["token_kind"] = tokenKind;

            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = issuer,
                Audience = audience,
                IssuedAt = now.UtcDateTime,
                NotBefore = now.UtcDateTime,
                Expires = now.AddMinutes(15).UtcDateTime,
                Claims = claims,
            };
            if (algorithm == SecurityAlgorithms.HmacSha256)
            {
                descriptor.SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(signingKey)), algorithm);
            }
            return new JsonWebTokenHandler().CreateToken(descriptor);
        }

        private static async Task<TokenValidatedContext> ValidateAndBindAsync(
            string rawToken,
            JwtBearerOptions options,
            ITenantContextAccessor? accessor = null,
            ListLogger? logger = null)
        {
            var validation = await new JsonWebTokenHandler().ValidateTokenAsync(rawToken, options.TokenValidationParameters);
            Assert.True(validation.IsValid, validation.Exception?.ToString());
            var httpContext = new DefaultHttpContext();
            httpContext.Response.Body = new MemoryStream();
            var services = new ServiceCollection();
            services.AddSingleton<ITenantContextAccessor>(accessor ?? new StubTenantContextAccessor(TenantContext.Create(TenantId, "tenant-secret-ref", true)));
            if (logger is not null) services.AddSingleton<ILogger<TenantJwtBearerEvents>>(logger);
            httpContext.RequestServices = services.BuildServiceProvider();
            var context = new TokenValidatedContext(
                httpContext,
                new AuthenticationScheme(TenantAuthenticationSchemes.Bearer, null, typeof(JwtBearerHandler)),
                options)
            {
                Principal = validation.ClaimsIdentity is null ? null : new ClaimsPrincipal(validation.ClaimsIdentity),
                SecurityToken = validation.SecurityToken,
            };
            await options.Events.TokenValidated(context);
            return context;
        }

        [Fact]
        public void TenantScheme_EnablesIssuerAudienceLifetimeSignatureAndHs256Validation()
        {
            var (tenant, _, _) = RegisterSchemes();
            var parameters = tenant.TokenValidationParameters;

            Assert.Equal("https://issuer.cafehub.test", parameters.ValidIssuer);
            Assert.Equal("tenant-api", parameters.ValidAudience);
            Assert.True(parameters.ValidateIssuer);
            Assert.True(parameters.ValidateAudience);
            Assert.True(parameters.ValidateLifetime);
            Assert.True(parameters.ValidateIssuerSigningKey);
            Assert.True(parameters.RequireExpirationTime);
            Assert.Equal(TimeSpan.FromSeconds(30), parameters.ClockSkew);
            Assert.Equal(SecurityAlgorithms.HmacSha256, Assert.Single(parameters.ValidAlgorithms!));
        }

        [Fact]
        public async Task MatchingTenantClaim_IsAuthenticated()
        {
            var (tenant, _, _) = RegisterSchemes();

            var context = await ValidateAndBindAsync(CreateTenantToken(), tenant);

            Assert.Null(context.Result);
        }

        [Fact]
        public async Task DifferentTenantClaim_IsRejectedAndLogsOnlyTenantMismatchCategory()
        {
            var logger = new ListLogger();
            var (tenant, _, _) = RegisterSchemes(logger: logger);

            var context = await ValidateAndBindAsync(CreateTenantToken(tenantId: OtherTenantId), tenant, logger: logger);

            Assert.NotNull(context.Result?.Failure);
            Assert.Contains(logger.Messages, message => message.Contains("tenant_mismatch", StringComparison.Ordinal));
            Assert.DoesNotContain(logger.Messages, message => message.Contains(OtherTenantId.ToString(), StringComparison.Ordinal));
        }

        [Theory]
        [InlineData("admin", true, null, "token_kind")]
        [InlineData("tenant", false, null, "tenant_mismatch")]
        [InlineData("tenant", true, "not-a-guid", "tenant_mismatch")]
        public async Task MissingOrInvalidBindingClaims_AreRejected(string tokenKind, bool hasTenantId, string? tenantId, string category)
        {
            var logger = new ListLogger();
            var (tenant, _, _) = RegisterSchemes(logger: logger);

            var context = await ValidateAndBindAsync(CreateTenantToken(tokenKind: tokenKind, includeTenantId: hasTenantId, tenantIdValue: tenantId), tenant, logger: logger);

            Assert.NotNull(context.Result?.Failure);
            Assert.Contains(logger.Messages, message => message.Contains(category, StringComparison.Ordinal));
        }

        [Fact]
        public async Task MissingTokenKind_IsRejected()
        {
            var logger = new ListLogger();
            var (tenant, _, _) = RegisterSchemes(logger: logger);

            var context = await ValidateAndBindAsync(CreateTenantToken(tokenKind: null), tenant, logger: logger);

            Assert.NotNull(context.Result?.Failure);
            Assert.Contains(logger.Messages, message => message.Contains("token_kind", StringComparison.Ordinal));
        }

        [Fact]
        public async Task NoResolvedTenantContext_FailsClosedAndLogsGenericCategory()
        {
            var logger = new ListLogger();
            var (tenant, _, _) = RegisterSchemes(new MissingTenantContextAccessor(), logger);

            var context = await ValidateAndBindAsync(CreateTenantToken(), tenant, new MissingTenantContextAccessor(), logger);

            Assert.NotNull(context.Result?.Failure);
            Assert.Contains(logger.Messages, message => message.Contains("no_tenant_context", StringComparison.Ordinal));
        }

        [Theory]
        [InlineData("wrong-key")]
        [InlineData("wrong-issuer")]
        [InlineData("platform-audience")]
        [InlineData("unsigned")]
        [InlineData("expired")]
        public async Task TenantScheme_RejectsInvalidSignatureIssuerAudienceAlgorithmAndLifetime(string invalidCase)
        {
            var (tenant, _, _) = RegisterSchemes();
            var token = invalidCase switch
            {
                "wrong-key" => CreateTenantToken(signingKey: "ffffffffffffffffffffffffffffffff"),
                "wrong-issuer" => CreateTenantToken(issuer: "https://other-issuer.cafehub.test"),
                "platform-audience" => CreateTenantToken(audience: "platform-api"),
                "unsigned" => CreateTenantToken(algorithm: SecurityAlgorithms.None),
                "expired" => CreateTenantToken(issuedAt: Now.AddHours(-1)),
                _ => throw new ArgumentOutOfRangeException(nameof(invalidCase)),
            };

            var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, tenant.TokenValidationParameters);

            Assert.False(result.IsValid);
        }

        [Fact]
        public async Task PlatformTokenIsRejectedByTenantSchemeAndTenantTokenByPlatformScheme()
        {
            var (tenant, platform, _) = RegisterSchemes();
            var platformToken = new JwtAccessTokenIssuer(PlatformOptions(), new FixedTimeProvider(Now)).Issue(new AccessTokenRequest
            {
                AdministratorId = Guid.NewGuid(),
                Email = "admin@example.com",
                Roles = new[] { "SuperAdmin" },
            });
            var tenantToken = CreateTenantToken();

            var platformAsTenant = await new JsonWebTokenHandler().ValidateTokenAsync(platformToken.Value, tenant.TokenValidationParameters);
            var tenantAsPlatform = await new JsonWebTokenHandler().ValidateTokenAsync(tenantToken, platform.TokenValidationParameters);

            Assert.False(platformAsTenant.IsValid);
            Assert.False(tenantAsPlatform.IsValid);
        }

        [Fact]
        public async Task BindingFailureUsesGeneric401ProblemDetailsAndDoesNotEchoToken()
        {
            var token = CreateTenantToken(tenantId: OtherTenantId);
            var logger = new ListLogger();
            var (tenant, _, _) = RegisterSchemes(logger: logger);
            var context = await ValidateAndBindAsync(token, tenant, logger: logger);
            var challengeContext = new JwtBearerChallengeContext(context.HttpContext, context.Scheme, tenant, new AuthenticationProperties());

            await tenant.Events.Challenge(challengeContext);

            Assert.Equal(StatusCodes.Status401Unauthorized, context.HttpContext.Response.StatusCode);
            Assert.Equal("application/problem+json", context.HttpContext.Response.ContentType);
            var stream = Assert.IsType<MemoryStream>(context.HttpContext.Response.Body);
            stream.Position = 0;
            using var json = await JsonDocument.ParseAsync(stream);
            Assert.Equal("Identity.NotAuthenticated", json.RootElement.GetProperty("code").GetString());
            stream.Position = 0;
            using var reader = new StreamReader(stream, leaveOpen: true);
            var response = await reader.ReadToEndAsync();
            Assert.DoesNotContain(token, response, StringComparison.Ordinal);
            Assert.DoesNotContain(logger.Messages, message => message.Contains(token, StringComparison.Ordinal));
        }

        [Fact]
        public void TenantScheme_DoesNotChangePlatformDefaultScheme()
        {
            var (tenant, _, services) = RegisterSchemes();
            using (services)
            {
                var authenticationOptions = services.GetRequiredService<IOptions<AuthenticationOptions>>().Value;
                Assert.Equal(JwtBearerDefaults.AuthenticationScheme, authenticationOptions.DefaultAuthenticateScheme);
                Assert.Equal(JwtBearerDefaults.AuthenticationScheme, authenticationOptions.DefaultChallengeScheme);
                Assert.Equal(TenantAuthenticationSchemes.Bearer, authenticationOptions.Schemes.Single(scheme => scheme.Name == TenantAuthenticationSchemes.Bearer).Name);
                Assert.NotNull(tenant.Events.OnTokenValidated);
            }
        }

        private sealed class StubTenantContextAccessor(TenantContext context) : ITenantContextAccessor
        {
            public TenantContext GetRequired() => context;
        }

        private sealed class MissingTenantContextAccessor : ITenantContextAccessor
        {
            public TenantContext GetRequired() => throw new TenantContextMissingException("missing");
        }

        private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => now;
        }

        private sealed class ListLogger : ILogger<TenantJwtBearerEvents>
        {
            public List<string> Messages { get; } = [];
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
        }
    }
}
