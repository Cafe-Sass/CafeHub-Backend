using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Tenants;
using CafeHub.WebApi.Tenancy;
using Microsoft.AspNetCore.Http;

namespace CafeHub.UnitTests.Isolation
{
    /// <summary>
    /// Proves tenant isolation at the request boundary.
    /// Note: The data-access boundary (a real database per tenant) is NOT proven by automated unit tests;
    /// it is covered by documented manual verification.
    /// </summary>
    public sealed class TenantRequestBoundaryIsolationTests
    {
        private static readonly Guid TenantAId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
        private static readonly Guid TenantBId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");

        [Fact]
        public async Task SequentialRequests_ResolveOnlyRespectiveTenantContext()
        {
            var registry = new Dictionary<string, ResolvedTenant>(StringComparer.Ordinal)
            {
                ["cafe-a"] = new ResolvedTenant(TenantAId, "cafe-a", TenantLifecycleStatus.Active, "ref-a"),
                ["cafe-b"] = new ResolvedTenant(TenantBId, "cafe-b", TenantLifecycleStatus.Active, "ref-b"),
            };
            var resolver = new FakeTenantResolver(registry);
            var guard = new TenantStateGuard();
            var middleware = new TenantResolutionMiddleware(_ => Task.CompletedTask);

            var setterA = new FakeTenantContextSetter();
            var contextA = CreateHttpContext("/t/cafe-a/menu");
            await middleware.InvokeAsync(contextA, resolver, guard, setterA);

            Assert.NotNull(setterA.Context);
            Assert.Equal(TenantAId, setterA.Context.TenantId);
            Assert.Equal("cafe-a", setterA.Context.Slug);

            var setterB = new FakeTenantContextSetter();
            var contextB = CreateHttpContext("/t/cafe-b/orders");
            await middleware.InvokeAsync(contextB, resolver, guard, setterB);

            Assert.NotNull(setterB.Context);
            Assert.Equal(TenantBId, setterB.Context.TenantId);
            Assert.Equal("cafe-b", setterB.Context.Slug);

            Assert.NotEqual(setterA.Context.TenantId, setterB.Context.TenantId);
        }

        [Fact]
        public async Task ConcurrentRequests_ResolveOnlyRespectiveTenantContextWithoutLeakage()
        {
            var registry = new Dictionary<string, ResolvedTenant>(StringComparer.Ordinal)
            {
                ["cafe-a"] = new ResolvedTenant(TenantAId, "cafe-a", TenantLifecycleStatus.Active, "ref-a"),
                ["cafe-b"] = new ResolvedTenant(TenantBId, "cafe-b", TenantLifecycleStatus.Active, "ref-b"),
            };
            var resolver = new FakeTenantResolver(registry);
            var guard = new TenantStateGuard();
            var middleware = new TenantResolutionMiddleware(_ => Task.CompletedTask);

            const int iterations = 50;
            var tasks = new Task[iterations * 2];

            for (var i = 0; i < iterations; i++)
            {
                tasks[i * 2] = Task.Run(async () =>
                {
                    var setter = new FakeTenantContextSetter();
                    var httpContext = CreateHttpContext("/t/cafe-a/menu");
                    await middleware.InvokeAsync(httpContext, resolver, guard, setter);

                    Assert.NotNull(setter.Context);
                    Assert.Equal(TenantAId, setter.Context.TenantId);
                    Assert.Equal("cafe-a", setter.Context.Slug);
                });

                tasks[(i * 2) + 1] = Task.Run(async () =>
                {
                    var setter = new FakeTenantContextSetter();
                    var httpContext = CreateHttpContext("/t/cafe-b/menu");
                    await middleware.InvokeAsync(httpContext, resolver, guard, setter);

                    Assert.NotNull(setter.Context);
                    Assert.Equal(TenantBId, setter.Context.TenantId);
                    Assert.Equal("cafe-b", setter.Context.Slug);
                });
            }

            await Task.WhenAll(tasks);
        }

        [Fact]
        public async Task RequestBoundary_IgnoresHeadersQueryStringsAndBodiesNamingAnotherTenant()
        {
            var registry = new Dictionary<string, ResolvedTenant>(StringComparer.Ordinal)
            {
                ["cafe-a"] = new ResolvedTenant(TenantAId, "cafe-a", TenantLifecycleStatus.Active, "ref-a"),
                ["cafe-b"] = new ResolvedTenant(TenantBId, "cafe-b", TenantLifecycleStatus.Active, "ref-b"),
            };
            var resolver = new FakeTenantResolver(registry);
            var guard = new TenantStateGuard();
            var middleware = new TenantResolutionMiddleware(_ => Task.CompletedTask);
            var setter = new FakeTenantContextSetter();

            var context = CreateHttpContext("/t/cafe-a/orders");
            context.Request.Headers["X-Tenant-Id"] = TenantBId.ToString();
            context.Request.Headers["X-Tenant"] = "cafe-b";
            context.Request.QueryString = new QueryString("?tenant=cafe-b&tenantId=" + TenantBId);

            var bodyJson = "{\"tenantId\":\"" + TenantBId + "\",\"slug\":\"cafe-b\"}";
            var bodyBytes = System.Text.Encoding.UTF8.GetBytes(bodyJson);
            context.Request.Body = new MemoryStream(bodyBytes);
            context.Request.ContentType = "application/json";
            context.Request.ContentLength = bodyBytes.Length;

            await middleware.InvokeAsync(context, resolver, guard, setter);

            Assert.NotNull(setter.Context);
            Assert.Equal(TenantAId, setter.Context.TenantId);
            Assert.Equal("cafe-a", setter.Context.Slug);
            Assert.NotEqual(TenantBId, setter.Context.TenantId);
        }

        private static DefaultHttpContext CreateHttpContext(string path)
        {
            var context = new DefaultHttpContext();
            context.Request.Path = path;
            context.Response.Body = new MemoryStream();
            return context;
        }

        private sealed class FakeTenantResolver(IReadOnlyDictionary<string, ResolvedTenant> tenants) : ITenantResolver
        {
            public Task<Result<ResolvedTenant>> ResolveAsync(string rawSlug, CancellationToken cancellationToken)
            {
                if (tenants.TryGetValue(rawSlug, out var tenant))
                {
                    return Task.FromResult(Result.Success(tenant));
                }

                return Task.FromResult(Result.Failure<ResolvedTenant>(TenancyErrors.TenantUnavailable()));
            }
        }

        private sealed class FakeTenantContextSetter : ITenantContextSetter
        {
            public TenantContext? Context { get; private set; }

            public void Set(TenantContext tenantContext)
            {
                Context = tenantContext;
            }
        }
    }
}
