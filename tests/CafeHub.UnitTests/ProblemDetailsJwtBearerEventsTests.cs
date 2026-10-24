using System.Text.Json;
using CafeHub.Application.Abstractions.Correlation;
using CafeHub.WebApi.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CafeHub.UnitTests
{
    public sealed class ProblemDetailsJwtBearerEventsTests
    {
        private const string SampleToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.e30.sample-token-value-not-a-real-secret";

        private static readonly AuthenticationScheme BearerScheme = new(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler));

        private static DefaultHttpContext CreateHttpContext(
            string traceIdentifier,
            ICorrelationIdAccessor? correlationIdAccessor = null,
            ListLogger? logger = null)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.TraceIdentifier = traceIdentifier;
            httpContext.Response.Body = new MemoryStream();

            var services = new ServiceCollection();
            if (correlationIdAccessor is not null)
            {
                services.AddSingleton<ICorrelationIdAccessor>(correlationIdAccessor);
            }

            if (logger is not null)
            {
                services.AddSingleton<ILogger<ProblemDetailsJwtBearerEvents>>(logger);
            }

            httpContext.RequestServices = services.BuildServiceProvider();
            return httpContext;
        }

        private static async Task<string> ReadBody(HttpContext httpContext)
        {
            var body = Assert.IsType<MemoryStream>(httpContext.Response.Body);
            body.Position = 0;
            using var reader = new StreamReader(body, leaveOpen: true);
            return await reader.ReadToEndAsync();
        }

        [Fact]
        public async Task Challenge_Returns401ProblemDetails_AndNeverEchoesTheToken()
        {
            var httpContext = CreateHttpContext("ctx-trace-challenge");
            httpContext.Request.Headers.Authorization = $"Bearer {SampleToken}";
            var context = new JwtBearerChallengeContext(httpContext, BearerScheme, new JwtBearerOptions(), new AuthenticationProperties());

            await new ProblemDetailsJwtBearerEvents().Challenge(context);

            Assert.Equal(StatusCodes.Status401Unauthorized, httpContext.Response.StatusCode);
            Assert.Equal("application/problem+json", httpContext.Response.ContentType);
            var body = await ReadBody(httpContext);
            using var json = JsonDocument.Parse(body);
            Assert.Equal(401, json.RootElement.GetProperty("status").GetInt32());
            Assert.Equal("Unauthorized", json.RootElement.GetProperty("title").GetString());
            Assert.Equal("Identity.NotAuthenticated", json.RootElement.GetProperty("code").GetString());
            Assert.Equal("Authentication is required to use this resource.", json.RootElement.GetProperty("detail").GetString());
            Assert.Equal("ctx-trace-challenge", json.RootElement.GetProperty("traceId").GetString());
            Assert.DoesNotContain(SampleToken, body, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Challenge_IncludesTheAccessorCorrelationId_WhenAvailable()
        {
            var httpContext = CreateHttpContext("ctx-trace-correlation", new FixedCorrelationIdAccessor("corr-from-accessor"));
            var context = new JwtBearerChallengeContext(httpContext, BearerScheme, new JwtBearerOptions(), new AuthenticationProperties());

            await new ProblemDetailsJwtBearerEvents().Challenge(context);

            var body = await ReadBody(httpContext);
            using var json = JsonDocument.Parse(body);
            Assert.Equal("corr-from-accessor", json.RootElement.GetProperty("correlationId").GetString());
        }

        [Fact]
        public async Task Challenge_OmitsTheCorrelationId_WhenTheAccessorHasNone()
        {
            var httpContext = CreateHttpContext("ctx-trace-omitted", new FixedCorrelationIdAccessor(null));
            var context = new JwtBearerChallengeContext(httpContext, BearerScheme, new JwtBearerOptions(), new AuthenticationProperties());

            await new ProblemDetailsJwtBearerEvents().Challenge(context);

            var body = await ReadBody(httpContext);
            using var json = JsonDocument.Parse(body);
            Assert.False(json.RootElement.TryGetProperty("correlationId", out _));
        }

        [Fact]
        public async Task Challenge_LogsOneWarningEntry_WithOnlyTheFailureCategory_AndNeverTheToken()
        {
            var logger = new ListLogger();
            var httpContext = CreateHttpContext("ctx-trace-log", logger: logger);
            httpContext.Request.Headers.Authorization = $"Bearer {SampleToken}";
            var context = new JwtBearerChallengeContext(httpContext, BearerScheme, new JwtBearerOptions(), new AuthenticationProperties());
            context.Error = "invalid_token";

            await new ProblemDetailsJwtBearerEvents().Challenge(context);

            var entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Null(entry.Exception);
            Assert.Contains("invalid_token", entry.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(SampleToken, entry.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Challenge_WithoutAToken_LogsTheMissingTokenCategory()
        {
            var logger = new ListLogger();
            var httpContext = CreateHttpContext("ctx-trace-missing", logger: logger);
            var context = new JwtBearerChallengeContext(httpContext, BearerScheme, new JwtBearerOptions(), new AuthenticationProperties());

            await new ProblemDetailsJwtBearerEvents().Challenge(context);

            var entry = Assert.Single(logger.Entries);
            Assert.Contains("missing_token", entry.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Forbidden_Returns403ProblemDetails_AndNeverEchoesTheToken()
        {
            var httpContext = CreateHttpContext("ctx-trace-forbidden", new FixedCorrelationIdAccessor("corr-forbidden"));
            httpContext.Request.Headers.Authorization = $"Bearer {SampleToken}";
            var context = new ForbiddenContext(httpContext, BearerScheme, new JwtBearerOptions());

            await new ProblemDetailsJwtBearerEvents().Forbidden(context);

            Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
            Assert.Equal("application/problem+json", httpContext.Response.ContentType);
            var body = await ReadBody(httpContext);
            using var json = JsonDocument.Parse(body);
            Assert.Equal(403, json.RootElement.GetProperty("status").GetInt32());
            Assert.Equal("Forbidden", json.RootElement.GetProperty("title").GetString());
            Assert.Equal("Identity.Forbidden", json.RootElement.GetProperty("code").GetString());
            Assert.Equal("You are not allowed to use this resource.", json.RootElement.GetProperty("detail").GetString());
            Assert.Equal("corr-forbidden", json.RootElement.GetProperty("correlationId").GetString());
            Assert.DoesNotContain(SampleToken, body, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Challenge_Throws_WhenTheContextIsMissing()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await new ProblemDetailsJwtBearerEvents().Challenge(null!));
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await new ProblemDetailsJwtBearerEvents().Forbidden(null!));
        }

        private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

        private sealed class ListLogger : ILogger<ProblemDetailsJwtBearerEvents>
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
