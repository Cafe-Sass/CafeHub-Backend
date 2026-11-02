using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.RateLimiting;
using CafeHub.Application.Abstractions.Correlation;
using CafeHub.WebApi.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace CafeHub.UnitTests.RateLimiting
{
    public sealed class RateLimitRejectionDispatcherTests
    {
        private static DefaultHttpContext CreateHttpContext(string traceIdentifier, string? correlationId = null)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.TraceIdentifier = traceIdentifier;
            httpContext.Response.Body = new MemoryStream();

            var services = new ServiceCollection();
            services.AddSingleton<ICorrelationIdAccessor>(sp => new TestCorrelationIdAccessor(correlationId));
            httpContext.RequestServices = services.BuildServiceProvider();

            return httpContext;
        }

        [Fact]
        public async Task WriteAsync_WithAdminAuthPolicy_DispatchesToAuthWriter()
        {
            var httpContext = CreateHttpContext("trace-auth");
            SetEndpointPolicy(httpContext, AuthRateLimiting.PolicyName);

            var context = new OnRejectedContext
            {
                HttpContext = httpContext,
                Lease = new FakeLease(),
            };

            await RateLimitRejectionDispatcher.WriteAsync(context, CancellationToken.None);

            var body = Assert.IsType<MemoryStream>(httpContext.Response.Body);
            body.Position = 0;
            using var reader = new StreamReader(body, leaveOpen: true);
            var bodyText = await reader.ReadToEndAsync();

            Assert.Contains("Auth.RateLimitExceeded", bodyText, StringComparison.Ordinal);
            Assert.DoesNotContain("Api.RateLimitExceeded", bodyText, StringComparison.Ordinal);
        }

        [Fact]
        public async Task WriteAsync_WithGlobalPolicy_DispatchesToGlobalWriter()
        {
            var httpContext = CreateHttpContext("trace-global");
            SetEndpointPolicy(httpContext, GlobalRateLimiting.PolicyName);

            var context = new OnRejectedContext
            {
                HttpContext = httpContext,
                Lease = new FakeLease(),
            };

            await RateLimitRejectionDispatcher.WriteAsync(context, CancellationToken.None);

            var body = Assert.IsType<MemoryStream>(httpContext.Response.Body);
            body.Position = 0;
            using var reader = new StreamReader(body, leaveOpen: true);
            var bodyText = await reader.ReadToEndAsync();

            Assert.Contains("Api.RateLimitExceeded", bodyText, StringComparison.Ordinal);
            Assert.DoesNotContain("Auth.RateLimitExceeded", bodyText, StringComparison.Ordinal);
        }

        [Fact]
        public async Task WriteAsync_WithNoPolicyMetadata_DispatchesToGlobalWriter()
        {
            var httpContext = CreateHttpContext("trace-none");

            var context = new OnRejectedContext
            {
                HttpContext = httpContext,
                Lease = new FakeLease(),
            };

            await RateLimitRejectionDispatcher.WriteAsync(context, CancellationToken.None);

            var body = Assert.IsType<MemoryStream>(httpContext.Response.Body);
            body.Position = 0;
            using var reader = new StreamReader(body, leaveOpen: true);
            var bodyText = await reader.ReadToEndAsync();

            Assert.Contains("Api.RateLimitExceeded", bodyText, StringComparison.Ordinal);
        }

        [Fact]
        public async Task WriteAsync_WithNullContext_Throws()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(
                async () => await RateLimitRejectionDispatcher.WriteAsync(null!, CancellationToken.None));
        }

        private static void SetEndpointPolicy(DefaultHttpContext httpContext, string policyName)
        {
            var metadata = new EndpointMetadataCollection(new EnableRateLimitingAttribute(policyName));
            var endpoint = new Endpoint(_ => Task.CompletedTask, metadata, "test-endpoint");
            httpContext.Features.Set<IEndpointFeature>(new TestEndpointFeature(endpoint));
        }

        private sealed class TestCorrelationIdAccessor : ICorrelationIdAccessor
        {
            public TestCorrelationIdAccessor(string? correlationId)
            {
                CorrelationId = correlationId;
            }

            public string? CorrelationId { get; }
        }

        private sealed class FakeLease : RateLimitLease
        {
            public override bool IsAcquired => false;

            public override IEnumerable<string> MetadataNames => Enumerable.Empty<string>();

            public override bool TryGetMetadata(string metadataName, out object? metadata)
            {
                metadata = null;
                return false;
            }
        }

        private sealed class TestEndpointFeature : IEndpointFeature
        {
            private Endpoint? _endpoint;

            public TestEndpointFeature(Endpoint endpoint)
            {
                _endpoint = endpoint;
            }

            Endpoint? IEndpointFeature.Endpoint
            {
                get => _endpoint;
                set => _endpoint = value;
            }
        }
    }
}
