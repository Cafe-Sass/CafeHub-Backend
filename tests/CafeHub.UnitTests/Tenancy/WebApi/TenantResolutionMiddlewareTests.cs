using System.Text.Json;
using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Tenants;
using CafeHub.WebApi.Tenancy;
using Microsoft.AspNetCore.Http;

namespace CafeHub.UnitTests.Tenancy.WebApi
{
    public sealed class TenantResolutionMiddlewareTests
    {
        [Fact]
        public async Task InvokeAsync_ResolvesUsableTenantSetsContextOnceAndContinues()
        {
            var id = Guid.NewGuid();
            var resolver = new Resolver(Result.Success(new ResolvedTenant(id, "cafe", TenantLifecycleStatus.Active, "ref")));
            var setter = new Setter();
            var nextCalled = false;
            var middleware = new TenantResolutionMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

            await middleware.InvokeAsync(Context("/t/cafe/menu"), resolver, new TenantStateGuard(), setter);

            Assert.True(nextCalled);
            Assert.Equal(1, setter.Count);
            Assert.Equal(id, setter.Context!.TenantId);
            Assert.Equal("cafe", setter.Context.Slug);
        }

        [Theory]
        [InlineData("/t")]
        [InlineData("/t/")]
        [InlineData("/t/bad.slug")]
        [InlineData("/t/unknown")]
        public async Task InvokeAsync_UnavailableTenantPathsHaveUniform404AndStop(string path)
        {
            var resolver = new Resolver(path.Contains("unknown", StringComparison.Ordinal)
                ? Result.Failure<ResolvedTenant>(TenancyErrors.TenantUnavailable())
                : Result.Success(new ResolvedTenant(Guid.NewGuid(), "slug", TenantLifecycleStatus.Suspended, "ref")));
            var setter = new Setter();
            var context = Context(path);
            var middleware = new TenantResolutionMiddleware(_ => throw new InvalidOperationException("next must not run"));

            await middleware.InvokeAsync(context, resolver, new TenantStateGuard(), setter);

            Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
            Assert.Equal("application/problem+json", context.Response.ContentType);
            Assert.Equal(0, setter.Count);
            context.Response.Body.Position = 0;
            using var json = await JsonDocument.ParseAsync(context.Response.Body);
            Assert.Equal("Tenant.Unavailable", json.RootElement.GetProperty("code").GetString());
        }

        [Theory]
        [InlineData("/api/v1/plans")]
        [InlineData("/team/cafe")]
        public async Task InvokeAsync_NonTenantPathsIgnoreTenantIdentifiers(string path)
        {
            var resolver = new Resolver(Result.Failure<ResolvedTenant>(TenancyErrors.TenantUnavailable()));
            var setter = new Setter();
            var context = Context(path);
            context.Request.Headers["X-Tenant"] = "cafe";
            context.Request.QueryString = new QueryString("?tenant=cafe");
            var nextCalled = false;
            var middleware = new TenantResolutionMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

            await middleware.InvokeAsync(context, resolver, new TenantStateGuard(), setter);

            Assert.True(nextCalled);
            Assert.Equal(0, resolver.CallCount);
            Assert.Equal(0, setter.Count);
        }

        private static DefaultHttpContext Context(string path)
        {
            var context = new DefaultHttpContext();
            context.Request.Path = path;
            context.Response.Body = new MemoryStream();
            return context;
        }

        private sealed class Resolver(Result<ResolvedTenant> result) : ITenantResolver
        {
            public int CallCount { get; private set; }
            public Task<Result<ResolvedTenant>> ResolveAsync(string rawSlug, CancellationToken cancellationToken)
            {
                CallCount++;
                return Task.FromResult(result);
            }
        }

        private sealed class Setter : ITenantContextSetter
        {
            public int Count { get; private set; }
            public TenantContext? Context { get; private set; }
            public void Set(TenantContext tenantContext) { Count++; Context = tenantContext; }
        }
    }
}
