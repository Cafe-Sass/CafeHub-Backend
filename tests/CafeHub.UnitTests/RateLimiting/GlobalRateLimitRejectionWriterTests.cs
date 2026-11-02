using System.Globalization;
using System.Text.Json;
using System.Threading.RateLimiting;
using CafeHub.Application.Abstractions.Correlation;
using CafeHub.WebApi.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

namespace CafeHub.UnitTests.RateLimiting
{
    public sealed class GlobalRateLimitRejectionWriterTests
    {
        private static DefaultHttpContext CreateHttpContext(string traceIdentifier, string? correlationId = null)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.TraceIdentifier = traceIdentifier;
            httpContext.Response.Body = new MemoryStream();

            var services = new ServiceCollection();
            services.AddSingleton<ICorrelationIdAccessor>(new FixedCorrelationIdAccessor(correlationId));
            httpContext.RequestServices = services.BuildServiceProvider();

            return httpContext;
        }

        private static async Task<JsonDocument> WriteAndParseAsync(HttpContext httpContext, RateLimitLease lease)
        {
            var context = new OnRejectedContext
            {
                HttpContext = httpContext,
                Lease = lease,
            };

            await GlobalRateLimitRejectionWriter.WriteAsync(context, CancellationToken.None);

            var body = Assert.IsType<MemoryStream>(httpContext.Response.Body);
            body.Position = 0;
            return await JsonDocument.ParseAsync(body);
        }

        [Fact]
        public async Task WriteAsync_WritesTheDocumented429ProblemDetails()
        {
            var httpContext = CreateHttpContext("trace-global-rate-limit");

            using var json = await WriteAndParseAsync(httpContext, new FakeLease());

            Assert.Equal(StatusCodes.Status429TooManyRequests, httpContext.Response.StatusCode);
            Assert.Equal("application/problem+json", httpContext.Response.ContentType);
            Assert.Equal(429, json.RootElement.GetProperty("status").GetInt32());
            Assert.Equal(GlobalRateLimiting.ProblemTitle, json.RootElement.GetProperty("title").GetString());
            Assert.Equal(GlobalRateLimiting.ProblemType, json.RootElement.GetProperty("type").GetString());
            Assert.Equal(GlobalRateLimiting.ErrorCode, json.RootElement.GetProperty("code").GetString());
            Assert.Equal(GlobalRateLimiting.ErrorDetail, json.RootElement.GetProperty("detail").GetString());
            Assert.Equal("trace-global-rate-limit", json.RootElement.GetProperty("traceId").GetString());
        }

        [Fact]
        public async Task WriteAsync_IncludesTheCorrelationId_WhenTheAccessorHasOne()
        {
            var httpContext = CreateHttpContext("trace-global-rate-limit-correlation", "corr-global-rate-limit");

            using var json = await WriteAndParseAsync(httpContext, new FakeLease());

            Assert.Equal("corr-global-rate-limit", json.RootElement.GetProperty("correlationId").GetString());
        }

        [Fact]
        public async Task WriteAsync_OmitsTheCorrelationId_WhenTheAccessorHasNone()
        {
            var httpContext = CreateHttpContext("trace-global-rate-limit-no-correlation", correlationId: null);

            using var json = await WriteAndParseAsync(httpContext, new FakeLease());

            Assert.False(json.RootElement.TryGetProperty("correlationId", out _));
        }

        [Fact]
        public async Task WriteAsync_RoundsTheRetryAfterUpToWholeSeconds()
        {
            var httpContext = CreateHttpContext("trace-global-rate-limit-retry");
            var lease = new FakeLease(new Dictionary<string, object?>
            {
                [MetadataName.RetryAfter.Name] = TimeSpan.FromSeconds(12.1),
            });

            using var json = await WriteAndParseAsync(httpContext, lease);

            Assert.Equal("13", httpContext.Response.Headers[HeaderNames.RetryAfter].ToString());
        }

        [Fact]
        public async Task WriteAsync_OmitsTheRetryAfterHeader_WhenTheLeaseHasNoMetadata()
        {
            var httpContext = CreateHttpContext("trace-global-rate-limit-no-retry");

            using var json = await WriteAndParseAsync(httpContext, new FakeLease());

            Assert.False(httpContext.Response.Headers.ContainsKey(HeaderNames.RetryAfter));
        }

        [Fact]
        public async Task WriteAsync_NeverEchoesRequestSecrets()
        {
            var httpContext = CreateHttpContext("trace-global-rate-limit-secrets");
            httpContext.Request.Headers.Authorization = "Bearer super-secret-access-token";
            httpContext.Request.QueryString = new QueryString("?refreshToken=super-secret-refresh-token");

            using var json = await WriteAndParseAsync(httpContext, new FakeLease());
            var body = httpContext.Response.Body is MemoryStream stream ? System.Text.Encoding.UTF8.GetString(stream.ToArray()) : string.Empty;

            Assert.DoesNotContain("super-secret-access-token", body, StringComparison.Ordinal);
            Assert.DoesNotContain("super-secret-refresh-token", body, StringComparison.Ordinal);
        }

        [Fact]
        public async Task WriteAsync_Throws_WhenTheContextOrRequiredMembersAreMissing()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await GlobalRateLimitRejectionWriter.WriteAsync(null!, CancellationToken.None));

            var httpContext = CreateHttpContext("trace-global-rate-limit-guard");
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await GlobalRateLimitRejectionWriter.WriteAsync(
                new OnRejectedContext { HttpContext = null!, Lease = new FakeLease() },
                CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await GlobalRateLimitRejectionWriter.WriteAsync(
                new OnRejectedContext { HttpContext = httpContext, Lease = null! },
                CancellationToken.None));
        }

        private sealed class FakeLease : RateLimitLease
        {
            private readonly Dictionary<string, object?> metadata;

            public FakeLease(IReadOnlyDictionary<string, object?>? metadata = null)
            {
                this.metadata = metadata is null
                    ? new Dictionary<string, object?>(StringComparer.Ordinal)
                    : new Dictionary<string, object?>(metadata, StringComparer.Ordinal);
            }

            public override bool IsAcquired => false;

            public override IEnumerable<string> MetadataNames => metadata.Keys;

            public override bool TryGetMetadata(string metadataName, out object? metadata)
            {
                return this.metadata.TryGetValue(metadataName, out metadata);
            }
        }
    }
}
