using System.Threading;
using CafeHub.WebApi.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace CafeHub.UnitTests.Security
{
    public sealed class SecurityHeadersMiddlewareTests
    {
        [Fact]
        public async Task InvokeAsync_WritesAllBaseHeaders_OnAnyResponse()
        {
            var context = CreateContext("/anything");
            await InvokeAndStart(context, "Development");

            Assert.True(context.Response.Headers.ContainsKey(SecurityHeaderValues.XContentTypeOptionsName));
            Assert.True(context.Response.Headers.ContainsKey(SecurityHeaderValues.XFrameOptionsName));
            Assert.True(context.Response.Headers.ContainsKey(SecurityHeaderValues.ReferrerPolicyName));
            Assert.True(context.Response.Headers.ContainsKey(SecurityHeaderValues.PermissionsPolicyName));

            Assert.Equal("nosniff", context.Response.Headers[SecurityHeaderValues.XContentTypeOptionsName]);
            Assert.Equal("DENY", context.Response.Headers[SecurityHeaderValues.XFrameOptionsName]);
            Assert.Equal("no-referrer", context.Response.Headers[SecurityHeaderValues.ReferrerPolicyName]);
            Assert.Equal("camera=(), microphone=(), geolocation=(), payment=()",
                context.Response.Headers[SecurityHeaderValues.PermissionsPolicyName]);
        }

        [Fact]
        public async Task InvokeAsync_WritesCspAndCacheControl_OnApiPath()
        {
            var context = CreateContext("/api/tenants");
            await InvokeAndStart(context, "Development");

            Assert.Equal("default-src 'none'; frame-ancestors 'none'",
                context.Response.Headers[SecurityHeaderValues.ContentSecurityPolicyName]);
            Assert.Equal("no-store", context.Response.Headers[SecurityHeaderValues.CacheControlName]);
        }

        [Fact]
        public async Task InvokeAsync_WritesCspAndCacheControl_OnTenantPath()
        {
            var context = CreateContext("/t/abc123/settings");
            await InvokeAndStart(context, "Development");

            Assert.Equal("default-src 'none'; frame-ancestors 'none'",
                context.Response.Headers[SecurityHeaderValues.ContentSecurityPolicyName]);
            Assert.Equal("no-store", context.Response.Headers[SecurityHeaderValues.CacheControlName]);
        }

        [Fact]
        public async Task InvokeAsync_DoesNotWriteCspAndCacheControl_OnUnrelatedPath()
        {
            var context = CreateContext("/health");
            await InvokeAndStart(context, "Development");

            Assert.False(context.Response.Headers.ContainsKey(SecurityHeaderValues.ContentSecurityPolicyName));
            Assert.False(context.Response.Headers.ContainsKey(SecurityHeaderValues.CacheControlName));
        }

        [Theory]
        [InlineData("/")]
        [InlineData("/openapi/v1.json")]
        [InlineData("/swagger")]
        [InlineData("/swagger/index.html")]
        public async Task InvokeAsync_Development_DocumentationPathsExemptFromCspAndCacheControl(string path)
        {
            var context = CreateContext(path);
            await InvokeAndStart(context, "Development");

            Assert.False(context.Response.Headers.ContainsKey(SecurityHeaderValues.ContentSecurityPolicyName));
            Assert.False(context.Response.Headers.ContainsKey(SecurityHeaderValues.CacheControlName));
        }

        [Theory]
        [InlineData("/")]
        [InlineData("/openapi/v1.json")]
        [InlineData("/swagger")]
        public async Task InvokeAsync_Production_DocumentationPathsDoNotGetCspAndCacheControl(string path)
        {
            var context = CreateContext(path);
            await InvokeAndStart(context, "Production");

            Assert.False(context.Response.Headers.ContainsKey(SecurityHeaderValues.ContentSecurityPolicyName));
            Assert.False(context.Response.Headers.ContainsKey(SecurityHeaderValues.CacheControlName));
        }

        [Fact]
        public async Task InvokeAsync_Production_ApiPathGetsCspAndCacheControl()
        {
            var context = CreateContext("/api/tenants");
            await InvokeAndStart(context, "Production");

            Assert.Equal("default-src 'none'; frame-ancestors 'none'",
                context.Response.Headers[SecurityHeaderValues.ContentSecurityPolicyName]);
            Assert.Equal("no-store", context.Response.Headers[SecurityHeaderValues.CacheControlName]);
        }

        [Fact]
        public async Task InvokeAsync_SetsBodySizeLimit_WhenFeatureIsWritable()
        {
            var feature = new FakeMaxRequestBodySizeFeature { IsReadOnly = false };
            var context = CreateContext("/api/tenants", feature);
            var options = new RequestLimitsOptions { MaxBodyBytes = 2097152 };
            var middleware = new SecurityHeadersMiddleware(async ctx =>
            {
                await ctx.Response.StartAsync(CancellationToken.None);
            }, new FakeHostEnvironment("Development"), options);

            await middleware.InvokeAsync(context);

            Assert.Equal(2097152, feature.MaxRequestBodySize);
        }

        [Fact]
        public async Task InvokeAsync_LeavesReadOnlyBodySizeFeatureUntouched()
        {
            var feature = new FakeMaxRequestBodySizeFeature { IsReadOnly = true, MaxRequestBodySize = 999 };
            var context = CreateContext("/api/tenants", feature);
            var options = new RequestLimitsOptions { MaxBodyBytes = 2097152 };
            var middleware = new SecurityHeadersMiddleware(async ctx =>
            {
                await ctx.Response.StartAsync(CancellationToken.None);
            }, new FakeHostEnvironment("Development"), options);

            await middleware.InvokeAsync(context);

            Assert.Equal(999, feature.MaxRequestBodySize);
        }

        [Fact]
        public async Task InvokeAsync_IgnoresMissingBodySizeFeature_WithoutError()
        {
            var context = CreateContext("/api/tenants");
            var options = new RequestLimitsOptions { MaxBodyBytes = 2097152 };
            var middleware = new SecurityHeadersMiddleware(async ctx =>
            {
                await ctx.Response.StartAsync(CancellationToken.None);
            }, new FakeHostEnvironment("Development"), options);

            await middleware.InvokeAsync(context);

            Assert.True(context.Response.Headers.ContainsKey(SecurityHeaderValues.XContentTypeOptionsName));
        }

        private static DefaultHttpContext CreateContext(string path, IHttpMaxRequestBodySizeFeature? bodySizeFeature = null)
        {
            var context = new DefaultHttpContext
            {
                Request = { Path = path },
                Response = { Body = new MemoryStream() },
            };
            context.TraceIdentifier = "trace-test";

            if (bodySizeFeature is not null)
            {
                context.Features.Set(bodySizeFeature);
            }

            return context;
        }

        private static async Task InvokeAndStart(DefaultHttpContext context, string environmentName)
        {
            var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask,
                new FakeHostEnvironment(environmentName), new RequestLimitsOptions());

            await middleware.InvokeAsync(context);
        }

        private sealed class FakeMaxRequestBodySizeFeature : IHttpMaxRequestBodySizeFeature
        {
            public long? MaxRequestBodySize { get; set; }

            public bool IsReadOnly { get; set; }
        }

        private sealed class FakeHostEnvironment : IHostEnvironment
        {
            public FakeHostEnvironment(string environmentName)
            {
                EnvironmentName = environmentName;
            }

            public string EnvironmentName { get; set; }

            public string ApplicationName { get; set; } = "CafeHub.UnitTests";

            public string ContentRootPath { get; set; } = string.Empty;

            public IFileProvider ContentRootFileProvider { get; set; } = null!;
        }
    }
}
