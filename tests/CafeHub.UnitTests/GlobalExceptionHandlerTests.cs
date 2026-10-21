using System.Text.Json;
using CafeHub.Application.Tenancy;
using CafeHub.WebApi.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CafeHub.UnitTests
{
    public sealed class GlobalExceptionHandlerTests
    {
        public static TheoryData<Exception, int, string, string> TenantIsolationCases => new()
        {
            { new TenantContextMissingException("No tenant context was set on this request."), StatusCodes.Status400BadRequest, "Tenant.ContextInvalid", "No tenant context was set on this request." },
            { new InvalidTenantContextException("A tenant context cannot be created with an empty tenant id."), StatusCodes.Status400BadRequest, "Tenant.ContextInvalid", "A tenant context cannot be created with an empty tenant id." },
            { new TenantInactiveException("Tenant 00000000-0000-0000-0000-000000000001 is not active. Activate the tenant before resolving its connection."), StatusCodes.Status404NotFound, "Tenant.Unavailable", "00000000-0000-0000-0000-000000000001" },
            { new TenantConnectionNotConfiguredException("TenantConnections:AlpHaConn was not configured because no value was set."), StatusCodes.Status404NotFound, "Tenant.Unavailable", "TenantConnections:AlpHaConn" },
        };

        [Theory]
        [MemberData(nameof(TenantIsolationCases))]
        public async Task TryHandleAsync_TenantIsolationException_ProducesTheProposedSafeResponse(Exception exception, int expectedStatus, string expectedCode, string forbiddenSnippet)
        {
            var httpContext = CreateHttpContext("ctx-trace-tenant");

            var handled = await new GlobalExceptionHandler(new ListLogger()).TryHandleAsync(httpContext, exception, CancellationToken.None);

            Assert.True(handled);
            Assert.Equal(expectedStatus, httpContext.Response.StatusCode);
            var body = await ReadBody(httpContext);
            using var json = JsonDocument.Parse(body);
            var expectedDetail = expectedStatus == StatusCodes.Status400BadRequest
                ? "A valid tenant is required to use this service."
                : "The requested tenant is not available.";
            Assert.Equal(expectedStatus, json.RootElement.GetProperty("status").GetInt32());
            Assert.Equal(expectedDetail, json.RootElement.GetProperty("detail").GetString());
            Assert.Equal(expectedCode, json.RootElement.GetProperty("code").GetString());
            Assert.Equal("ctx-trace-tenant", json.RootElement.GetProperty("traceId").GetString());
            Assert.DoesNotContain(forbiddenSnippet, body, StringComparison.Ordinal);
        }

        [Fact]
        public async Task TryHandleAsync_InactiveTenant_AndUnconfiguredConnection_ProduceIdenticalResponses()
        {
            var inactiveContext = CreateHttpContext("ctx-trace-shared");
            var unconfiguredContext = CreateHttpContext("ctx-trace-shared");
            var handler = new GlobalExceptionHandler(new ListLogger());

            var firstHandled = await handler.TryHandleAsync(inactiveContext, new TenantInactiveException("Tenant 00000000-0000-0000-0000-000000000001 is not active. Activate the tenant before resolving its connection."), CancellationToken.None);
            var secondHandled = await handler.TryHandleAsync(unconfiguredContext, new TenantConnectionNotConfiguredException("TenantConnections:AlpHaConn was not configured because no value was set."), CancellationToken.None);

            Assert.True(firstHandled);
            Assert.True(secondHandled);
            Assert.Equal(StatusCodes.Status404NotFound, inactiveContext.Response.StatusCode);
            Assert.Equal(StatusCodes.Status404NotFound, unconfiguredContext.Response.StatusCode);
            var firstBody = await ReadBody(inactiveContext);
            var secondBody = await ReadBody(unconfiguredContext);
            Assert.Equal(firstBody, secondBody);
            Assert.Contains("The requested tenant is not available.", firstBody, StringComparison.Ordinal);
            Assert.Contains("Tenant.Unavailable", firstBody, StringComparison.Ordinal);
            Assert.DoesNotContain("00000000-0000-0000-0000-000000000001", firstBody, StringComparison.Ordinal);
            Assert.DoesNotContain("TenantConnections:AlpHaConn", firstBody, StringComparison.Ordinal);
        }

        [Fact]
        public async Task TryHandleAsync_UnexpectedException_Returns500_WithFixedGenericBody_AndNoInternalDetails()
        {
            var httpContext = CreateHttpContext("ctx-trace-unexpected");
            var exception = new InvalidOperationException("internal boom detail about the request state");

            var handled = await new GlobalExceptionHandler(new ListLogger()).TryHandleAsync(httpContext, exception, CancellationToken.None);

            Assert.True(handled);
            Assert.Equal(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);
            var body = await ReadBody(httpContext);
            using var json = JsonDocument.Parse(body);
            Assert.Equal(500, json.RootElement.GetProperty("status").GetInt32());
            Assert.Equal("Internal Server Error", json.RootElement.GetProperty("title").GetString());
            Assert.Equal("An unexpected error occurred. Try again later.", json.RootElement.GetProperty("detail").GetString());
            Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.6.1", json.RootElement.GetProperty("type").GetString());
            Assert.Equal("Server.Unexpected", json.RootElement.GetProperty("code").GetString());
            Assert.Equal("ctx-trace-unexpected", json.RootElement.GetProperty("traceId").GetString());
            Assert.DoesNotContain("internal boom detail", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("InvalidOperationException", body, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task TryHandleAsync_UnexpectedException_LogsTheException_ExactlyOncePerRequest_WithoutInternalDetails()
        {
            var listLogger = new ListLogger();
            var exception = new InvalidOperationException("internal boom detail about the request state");

            await new GlobalExceptionHandler(listLogger).TryHandleAsync(CreateHttpContext("ctx-trace-log"), exception, CancellationToken.None);

            var entry = Assert.Single(listLogger.Entries);
            Assert.Equal(LogLevel.Error, entry.Level);
            Assert.Same(exception, entry.Exception);
            Assert.Contains("ctx-trace-log", entry.Message, StringComparison.Ordinal);
            Assert.Contains("InvalidOperationException", entry.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("internal boom detail", entry.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task TryHandleAsync_TenantIsolationException_LogsTheException_ExactlyOnce_WithoutSettingValues()
        {
            var listLogger = new ListLogger();

            await new GlobalExceptionHandler(listLogger).TryHandleAsync(CreateHttpContext("ctx-trace-log"), new TenantInactiveException("Tenant 00000000-0000-0000-0000-000000000001 is not active. Activate the tenant before resolving its connection."), CancellationToken.None);

            var entry = Assert.Single(listLogger.Entries);
            Assert.Equal(LogLevel.Error, entry.Level);
            Assert.DoesNotContain("TenantConnections:AlpHaConn", entry.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("internal boom detail", entry.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task TryHandleAsync_Throws_WhenContextOrExceptionIsMissing()
        {
            var handler = new GlobalExceptionHandler(new ListLogger());

            await Assert.ThrowsAsync<ArgumentNullException>(async () => await handler.TryHandleAsync(httpContext: null!, new InvalidOperationException("internal boom detail"), CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await handler.TryHandleAsync(CreateHttpContext("ctx-trace-guard"), exception: null!, CancellationToken.None));
        }

        private static DefaultHttpContext CreateHttpContext(string traceIdentifier)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.TraceIdentifier = traceIdentifier;
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

        private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

        private sealed class ListLogger : ILogger<GlobalExceptionHandler>
        {
            public List<LogEntry> Entries { get; } = new();

            private sealed class NoopDisposable : IDisposable
            {
                public static readonly NoopDisposable Instance = new();

                public void Dispose()
                {
                }
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            {
                return NoopDisposable.Instance;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
            }
        }
    }
}
