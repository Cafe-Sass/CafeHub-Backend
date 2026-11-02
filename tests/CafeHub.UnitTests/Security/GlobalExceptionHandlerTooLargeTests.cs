using System;
using System.Text.Json;
using System.Threading;
using CafeHub.Application.Abstractions.Correlation;
using CafeHub.WebApi.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CafeHub.UnitTests.Security
{
    public sealed class GlobalExceptionHandlerTooLargeTests
    {
        [Fact]
        public async Task TryHandleAsync_BadHttpRequestException413_Returns413_WithRequestTooLargeCode_AndNoLimitValues()
        {
            var httpContext = CreateHttpContext("ctx-trace-too-large");
            var handler = new GlobalExceptionHandler(new ListLogger());

            var handled = await handler.TryHandleAsync(httpContext,
                new BadHttpRequestException("The request body is too large.", StatusCodes.Status413RequestEntityTooLarge),
                CancellationToken.None);

            Assert.True(handled);
            Assert.Equal(StatusCodes.Status413RequestEntityTooLarge, httpContext.Response.StatusCode);

            var body = await ReadBody(httpContext);
            using var json = JsonDocument.Parse(body);
            Assert.Equal(413, json.RootElement.GetProperty("status").GetInt32());
            Assert.Equal("Request Entity Too Large", json.RootElement.GetProperty("title").GetString());
            Assert.Equal("The request body is too large.", json.RootElement.GetProperty("detail").GetString());
            Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.5.13", json.RootElement.GetProperty("type").GetString());
            Assert.Equal("Request.TooLarge", json.RootElement.GetProperty("code").GetString());
            Assert.Equal("ctx-trace-too-large", json.RootElement.GetProperty("traceId").GetString());
            Assert.False(json.RootElement.TryGetProperty("correlationId", out _));

            Assert.DoesNotContain("1048576", body, StringComparison.Ordinal);
            Assert.DoesNotContain("32768", body, StringComparison.Ordinal);
            Assert.DoesNotContain("MaxBodyBytes", body, StringComparison.Ordinal);
            Assert.DoesNotContain("MaxHeadersTotalBytes", body, StringComparison.Ordinal);
        }

        [Fact]
        public async Task TryHandleAsync_BadHttpRequestException413_IncludesCorrelationId_WhenAvailable()
        {
            var httpContext = CreateHttpContext("ctx-trace-correlation");
            var handler = new GlobalExceptionHandler(new ListLogger(), new FixedCorrelationIdAccessor("corr-from-accessor"));

            var handled = await handler.TryHandleAsync(httpContext,
                new BadHttpRequestException("The request body is too large.", StatusCodes.Status413RequestEntityTooLarge),
                CancellationToken.None);

            Assert.True(handled);
            var body = await ReadBody(httpContext);
            using var json = JsonDocument.Parse(body);
            Assert.Equal("corr-from-accessor", json.RootElement.GetProperty("correlationId").GetString());
            Assert.Equal(413, json.RootElement.GetProperty("status").GetInt32());
        }

        private static DefaultHttpContext CreateHttpContext(string traceIdentifier)
        {
            var httpContext = new DefaultHttpContext
            {
                TraceIdentifier = traceIdentifier,
                Response = { Body = new MemoryStream() },
            };
            return httpContext;
        }

        private static async Task<string> ReadBody(HttpContext httpContext)
        {
            var body = Assert.IsType<MemoryStream>(httpContext.Response.Body);
            body.Position = 0;
            using var reader = new StreamReader(body, leaveOpen: true);
            return await reader.ReadToEndAsync();
        }

        private sealed class ListLogger : ILogger<GlobalExceptionHandler>
        {
            public List<LogEntry> Entries { get; } = new();

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null!;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                Entries.Add(new(logLevel, formatter(state, exception), exception, 0));
            }

            public sealed record LogEntry(LogLevel Level, string Message, Exception? Exception, int ActiveScopeCount);
        }
        private sealed class FixedCorrelationIdAccessor : ICorrelationIdAccessor
        {
            public FixedCorrelationIdAccessor(string? correlationId)
            {
                CorrelationId = correlationId;
            }

            public string? CorrelationId { get; }
        }
    }
}
