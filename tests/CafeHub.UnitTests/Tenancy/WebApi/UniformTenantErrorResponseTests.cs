using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Tenants;
using CafeHub.WebApi;
using CafeHub.WebApi.Authentication;
using CafeHub.WebApi.Tenancy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CafeHub.UnitTests.Tenancy.WebApi
{
    /// <summary>
    /// Proves that every way a tenant can be unavailable to a caller produces one identical RFC 9457
    /// response: an unknown slug, a malformed slug, an unusable tenant, and a tenant token bound to a
    /// different tenant. Only the instance and correlation identifier members may differ.
    /// </summary>
    public sealed class UniformTenantErrorResponseTests
    {
        private const string TraceIdentifier = "uniform-tenant-trace";
        private const string CorrelationId = "uniform-tenant-correlation";

        private static readonly Guid ActiveTenantId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
        private static readonly Guid OtherTenantId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
        private static readonly AuthenticationScheme TenantScheme = new(TenantAuthenticationSchemes.Bearer, null, typeof(JwtBearerHandler));

        [Fact]
        public async Task UnknownMalformedUnusableAndForeignTenantToken_ReturnIdenticalProblemDetails()
        {
            var previousActivity = Activity.Current;
            Activity.Current = null;

            try
            {
                var responses = new[]
                {
                    await ResolveAsync("/t/bad.slug", Result.Failure<ResolvedTenant>(TenancyErrors.TenantUnavailable()), CorrelationId),
                    await ResolveAsync("/t/unknown", Result.Failure<ResolvedTenant>(TenancyErrors.TenantUnavailable()), CorrelationId),
                    await ResolveAsync("/t/unusable", Result.Success(new ResolvedTenant(ActiveTenantId, "unusable", TenantLifecycleStatus.Suspended, "ref")), CorrelationId),
                    await ChallengeForeignTenantTokenAsync(CorrelationId),
                };

                foreach (var response in responses)
                {
                    AssertTenantUnavailable(response);
                    ProblemDetailsAssertions.AssertEquivalent(responses[0].Body, response.Body);
                }
            }
            finally
            {
                Activity.Current = previousActivity;
            }
        }

        [Fact]
        public async Task EveryUniformTenantError_KeepsItsOwnCorrelationIdentifier()
        {
            var previousActivity = Activity.Current;
            Activity.Current = null;

            try
            {
                var responses = new[]
                {
                    await ResolveAsync("/t/bad.slug", Result.Failure<ResolvedTenant>(TenancyErrors.TenantUnavailable()), "corr-malformed"),
                    await ResolveAsync("/t/unknown", Result.Failure<ResolvedTenant>(TenancyErrors.TenantUnavailable()), "corr-unknown"),
                    await ResolveAsync("/t/unusable", Result.Success(new ResolvedTenant(ActiveTenantId, "unusable", TenantLifecycleStatus.Suspended, "ref")), "corr-unusable"),
                    await ChallengeForeignTenantTokenAsync("corr-token"),
                };

                Assert.Equal(
                    new[] { "corr-malformed", "corr-unknown", "corr-unusable", "corr-token" },
                    responses.Select(response => ReadCorrelationId(response.Body)));

                foreach (var response in responses)
                {
                    ProblemDetailsAssertions.AssertEquivalent(responses[0].Body, response.Body);
                }
            }
            finally
            {
                Activity.Current = previousActivity;
            }
        }

        private static async Task<TenantResponse> ResolveAsync(string path, Result<ResolvedTenant> resolution, string correlationId)
        {
            var context = CreateContext(path);
            var setter = new Setter();
            var middleware = new TenantResolutionMiddleware(_ => throw new InvalidOperationException("next must not run"));

            await middleware.InvokeAsync(
                context,
                new Resolver(resolution),
                new TenantStateGuard(),
                setter,
                correlationIdAccessor: new FixedCorrelationIdAccessor(correlationId));

            Assert.Equal(0, setter.Count);

            return await ReadResponseAsync(context);
        }

        private static async Task<TenantResponse> ChallengeForeignTenantTokenAsync(string correlationId)
        {
            var context = CreateContext();
            var services = new ServiceCollection();
            services.AddSingleton<ITenantContextAccessor>(
                new StubTenantContextAccessor(TenantContext.Create(ActiveTenantId, "tenant-secret-reference", true, "cafe-active")));
            services.AddSingleton<ICorrelationIdAccessor>(new FixedCorrelationIdAccessor(correlationId));
            context.RequestServices = services.BuildServiceProvider();

            var events = new TenantJwtBearerEvents();
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
                new[]
                {
                    new Claim("token_kind", "tenant"),
                    new Claim("tid", OtherTenantId.ToString()),
                },
                authenticationType: "test"));

            var tokenValidatedContext = new TokenValidatedContext(context, TenantScheme, new JwtBearerOptions())
            {
                Principal = principal,
            };

            await events.TokenValidated(tokenValidatedContext);
            Assert.NotNull(tokenValidatedContext.Result);

            var challengeContext = new JwtBearerChallengeContext(context, TenantScheme, new JwtBearerOptions(), new AuthenticationProperties());
            await events.Challenge(challengeContext);

            return await ReadResponseAsync(context);
        }

        private static void AssertTenantUnavailable(TenantResponse response)
        {
            Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
            Assert.Equal("application/problem+json", response.ContentType);

            using var json = JsonDocument.Parse(response.Body);
            Assert.Equal(404, json.RootElement.GetProperty("status").GetInt32());
            Assert.Equal("Not Found", json.RootElement.GetProperty("title").GetString());
            Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.5.5", json.RootElement.GetProperty("type").GetString());
            Assert.Equal("Tenant.Unavailable", json.RootElement.GetProperty("code").GetString());
            Assert.Equal("The requested tenant is not available.", json.RootElement.GetProperty("detail").GetString());
            Assert.Equal(TraceIdentifier, json.RootElement.GetProperty("traceId").GetString());
        }

        private static string? ReadCorrelationId(string body)
        {
            using var json = JsonDocument.Parse(body);
            return json.RootElement.GetProperty("correlationId").GetString();
        }

        private static DefaultHttpContext CreateContext(string? path = null)
        {
            var context = new DefaultHttpContext();
            context.TraceIdentifier = TraceIdentifier;
            context.Response.Body = new MemoryStream();

            if (path is not null)
            {
                context.Request.Path = path;
            }

            return context;
        }

        private static async Task<TenantResponse> ReadResponseAsync(HttpContext context)
        {
            var body = Assert.IsType<MemoryStream>(context.Response.Body);
            body.Position = 0;
            using var reader = new StreamReader(body, leaveOpen: true);
            var responseBody = await reader.ReadToEndAsync();
            return new TenantResponse(context.Response.StatusCode, context.Response.ContentType, responseBody);
        }

        private sealed record TenantResponse(int StatusCode, string? ContentType, string Body);

        private sealed class Resolver(Result<ResolvedTenant> resolution) : ITenantResolver
        {
            public Task<Result<ResolvedTenant>> ResolveAsync(string rawSlug, CancellationToken cancellationToken)
            {
                return Task.FromResult(resolution);
            }
        }

        private sealed class Setter : ITenantContextSetter
        {
            public int Count { get; private set; }

            public void Set(TenantContext tenantContext)
            {
                Count++;
            }
        }

        private sealed class StubTenantContextAccessor(TenantContext tenantContext) : ITenantContextAccessor
        {
            public TenantContext GetRequired()
            {
                return tenantContext;
            }
        }
    }
}
