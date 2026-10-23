using System.Text.Json;
using CafeHub.WebApi.Correlation;
using CafeHub.WebApi.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CafeHub.UnitTests
{
    public sealed class CorrelationIdMiddlewareTests
    {
        public static TheoryData<string> DisallowedValues => new()
        {
            { "cafe hub" },
            { "cafe/hub" },
            { "cafe\x001Fhub" },
            { "caféhub" },
            { "cafe\"hub" },
            { "cafe\nhub" },
        };

        [Fact]
        public async Task InvokeAsync_AValidHeaderAtTheMaximumLength_IsReused()
        {
            var httpContext = CreateHttpContext();
            var maximumLengthValue = new string('a', CorrelationIdPolicy.MaximumLength);
            httpContext.Request.Headers[CorrelationIdConstants.HeaderName] = maximumLengthValue;
            var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask, new FakeLogger<CorrelationIdMiddleware>(), new FixedCorrelationIdGenerator("unit-test-generated-value"));

            await middleware.InvokeAsync(httpContext);
            await httpContext.Response.WriteAsync("done");

            Assert.Equal(maximumLengthValue, httpContext.Response.Headers[CorrelationIdConstants.HeaderName].ToString());
            Assert.Equal(maximumLengthValue, httpContext.Items[CorrelationIdConstants.ItemKey]);
        }

        [Fact]
        public async Task InvokeAsync_AHeaderAboveTheMaximumLength_IsReplacedWithAGeneratedValue()
        {
            var httpContext = CreateHttpContext();
            httpContext.Request.Headers[CorrelationIdConstants.HeaderName] = new string('a', CorrelationIdPolicy.MaximumLength + 1);
            var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask, new FakeLogger<CorrelationIdMiddleware>(), new FixedCorrelationIdGenerator("unit-test-generated-value"));

            await middleware.InvokeAsync(httpContext);
            await httpContext.Response.WriteAsync("done");

            Assert.Equal("unit-test-generated-value", httpContext.Response.Headers[CorrelationIdConstants.HeaderName].ToString());
            Assert.Equal("unit-test-generated-value", httpContext.Items[CorrelationIdConstants.ItemKey]);
        }

        [Theory]
        [MemberData(nameof(DisallowedValues))]
        public async Task InvokeAsync_AHeaderWithADisallowedCharacter_IsReplacedWithAGeneratedValue(string rejectedValue)
        {
            var httpContext = CreateHttpContext();
            httpContext.Request.Headers[CorrelationIdConstants.HeaderName] = rejectedValue;
            var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask, new FakeLogger<CorrelationIdMiddleware>(), new FixedCorrelationIdGenerator("unit-test-generated-value"));

            await middleware.InvokeAsync(httpContext);
            await httpContext.Response.WriteAsync("done");

            Assert.Equal("unit-test-generated-value", httpContext.Response.Headers[CorrelationIdConstants.HeaderName].ToString());
            Assert.Equal("unit-test-generated-value", httpContext.Items[CorrelationIdConstants.ItemKey]);
        }

        [Fact]
        public async Task InvokeAsync_AMissingHeader_GeneratesAValue()
        {
            var httpContext = CreateHttpContext();
            var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask, new FakeLogger<CorrelationIdMiddleware>(), new FixedCorrelationIdGenerator("unit-test-generated-value"));

            await middleware.InvokeAsync(httpContext);
            await httpContext.Response.WriteAsync("done");

            Assert.Equal("unit-test-generated-value", httpContext.Response.Headers[CorrelationIdConstants.HeaderName].ToString());
            Assert.Equal("unit-test-generated-value", httpContext.Items[CorrelationIdConstants.ItemKey]);
        }

        [Fact]
        public async Task InvokeAsync_AnEmptyHeader_GeneratesAValue()
        {
            var httpContext = CreateHttpContext();
            httpContext.Request.Headers[CorrelationIdConstants.HeaderName] = string.Empty;
            var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask, new FakeLogger<CorrelationIdMiddleware>(), new FixedCorrelationIdGenerator("unit-test-generated-value"));

            await middleware.InvokeAsync(httpContext);
            await httpContext.Response.WriteAsync("done");

            Assert.Equal("unit-test-generated-value", httpContext.Response.Headers[CorrelationIdConstants.HeaderName].ToString());
            Assert.Equal("unit-test-generated-value", httpContext.Items[CorrelationIdConstants.ItemKey]);
        }

        [Fact]
        public async Task InvokeAsync_AWhitespaceOnlyHeader_GeneratesAValue()
        {
            var httpContext = CreateHttpContext();
            httpContext.Request.Headers[CorrelationIdConstants.HeaderName] = "   ";
            var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask, new FakeLogger<CorrelationIdMiddleware>(), new FixedCorrelationIdGenerator("unit-test-generated-value"));

            await middleware.InvokeAsync(httpContext);
            await httpContext.Response.WriteAsync("done");

            Assert.Equal("unit-test-generated-value", httpContext.Response.Headers[CorrelationIdConstants.HeaderName].ToString());
            Assert.Equal("unit-test-generated-value", httpContext.Items[CorrelationIdConstants.ItemKey]);
        }

        [Fact]
        public async Task InvokeAsync_TheLoggingScopeCarriesTheReusedIdentifier_AndClosesAfterTheRequest()
        {
            var httpContext = CreateHttpContext();
            httpContext.Request.Headers[CorrelationIdConstants.HeaderName] = "valid-correlation-id-1";
            var logger = new FakeLogger<CorrelationIdMiddleware>();
            var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask, logger, new FixedCorrelationIdGenerator("unit-test-generated-value"));

            await middleware.InvokeAsync(httpContext);

            var scope = Assert.Single(logger.Scopes);
            Assert.True(scope.IsDisposed);
            var values = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(scope.State);
            Assert.Equal("valid-correlation-id-1", values["CorrelationId"]);
        }

        [Fact]
        public async Task InvokeAsync_TheLoggingScopeCarriesTheGeneratedIdentifier()
        {
            var httpContext = CreateHttpContext();
            var logger = new FakeLogger<CorrelationIdMiddleware>();
            var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask, logger, new FixedCorrelationIdGenerator("unit-test-generated-value"));

            await middleware.InvokeAsync(httpContext);

            var scope = Assert.Single(logger.Scopes);
            var values = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(scope.State);
            Assert.Equal("unit-test-generated-value", values["CorrelationId"]);
        }

        [Fact]
        public async Task InvokeAsync_RejectedHeaderValues_NeverAppearInLoggedEntries()
        {
            var httpContext = CreateHttpContext();
            var rejectedValue = new string('*', 96) + "<script> /\\\"" + Environment.NewLine + "\t é";
            httpContext.Request.Headers[CorrelationIdConstants.HeaderName] = rejectedValue;
            var logger = new FakeLogger<CorrelationIdMiddleware>();
            var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask, logger, new FixedCorrelationIdGenerator("unit-test-generated-value"));

            await middleware.InvokeAsync(httpContext);

            Assert.All(logger.Entries, entry => Assert.DoesNotContain(rejectedValue, entry.Message, StringComparison.Ordinal));
            var scope = Assert.Single(logger.Scopes);
            var values = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(scope.State);
            Assert.Equal("unit-test-generated-value", values["CorrelationId"]);
        }

        [Fact]
        public async Task InvokeAsync_AnErrorResponseWrittenByTheNextStep_StillCarriesTheCorrelationHeaderAndBody()
        {
            var httpContext = CreateHttpContext();
            httpContext.Request.Headers[CorrelationIdConstants.HeaderName] = "valid-correlation-id-1";
            var accessor = new HttpContextCorrelationIdAccessor(new HttpContextAccessor { HttpContext = httpContext });
            var handler = new GlobalExceptionHandler(new FakeLogger<GlobalExceptionHandler>(), accessor);
            RequestDelegate next = async _ => await handler.TryHandleAsync(httpContext, new InvalidOperationException("internal boom detail about the request state"), CancellationToken.None);
            var middleware = new CorrelationIdMiddleware(next, new FakeLogger<CorrelationIdMiddleware>(), new FixedCorrelationIdGenerator("unit-test-generated-value"));

            await middleware.InvokeAsync(httpContext);

            Assert.Equal("valid-correlation-id-1", httpContext.Response.Headers[CorrelationIdConstants.HeaderName].ToString());
            var body = await ReadBody(httpContext);
            using var json = JsonDocument.Parse(body);
            Assert.Equal("valid-correlation-id-1", json.RootElement.GetProperty("correlationId").GetString());
        }

        [Fact]
        public void Constructor_Throws_WhenAnyDependencyIsMissing()
        {
            RequestDelegate next = _ => Task.CompletedTask;
            var logger = new FakeLogger<CorrelationIdMiddleware>();
            ICorrelationIdGenerator generator = new FixedCorrelationIdGenerator("unit-test-generated-value");

            Assert.Throws<ArgumentNullException>(() => new CorrelationIdMiddleware(null!, logger, generator));
            Assert.Throws<ArgumentNullException>(() => new CorrelationIdMiddleware(next, null!, generator));
            Assert.Throws<ArgumentNullException>(() => new CorrelationIdMiddleware(next, logger, null!));
        }

        [Fact]
        public async Task InvokeAsync_Throws_WhenTheHttpContextIsMissing()
        {
            var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask, new FakeLogger<CorrelationIdMiddleware>(), new FixedCorrelationIdGenerator("unit-test-generated-value"));

            await Assert.ThrowsAsync<ArgumentNullException>(() => middleware.InvokeAsync(null!));
        }

        private static DefaultHttpContext CreateHttpContext()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Response.Body = new MemoryStream();
            return httpContext;
        }

        private static async Task<string> ReadBody(HttpContext httpContext)
        {
            var body = Assert.IsType<MemoryStream>(httpContext.Response.Body);
            body.Position = 0;
            using var reader = new StreamReader(body, leaveOpen: true);
            return await reader.ReadToEndAsync();
        }

        private sealed class FixedCorrelationIdGenerator : ICorrelationIdGenerator
        {
            private readonly string generatedValue;

            public FixedCorrelationIdGenerator(string generatedValue)
            {
                this.generatedValue = generatedValue;
            }

            public string Generate()
            {
                return this.generatedValue;
            }
        }
    }
}
