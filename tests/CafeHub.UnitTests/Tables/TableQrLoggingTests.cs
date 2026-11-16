using CafeHub.WebApi.Security;
using Microsoft.Extensions.Logging;

namespace CafeHub.UnitTests.Tables
{
    public sealed class TableQrLoggingTests
    {
        [Fact]
        public void ProtectTableQrLogs_ClosesHostingScopesDespiteProviderSpecificRules()
        {
            var provider = new ScopedLogProvider();
            using var factory = LoggerFactory.Create(logging =>
            {
                logging.AddProvider(provider);
                logging.AddFilter<ScopedLogProvider>(level => level >= LogLevel.Warning);
                logging.AddFilter<ScopedLogProvider>("Microsoft.AspNetCore.Server.Kestrel.BadRequests", LogLevel.Trace);
                logging.AddFilter<ScopedLogProvider>("Microsoft.AspNetCore.Mvc.ModelBinding", LogLevel.Trace);
                logging.ProtectTableQrLogs();
            });
            var hosting = factory.CreateLogger("Microsoft.AspNetCore.Hosting.Diagnostics");
            const string rawPath = "/t/test-cafe/api/v1/guest/qr/Abcdefghijklmnopqrstuv";

            // ASP.NET creates this scope when the hosting category is enabled at Critical.
            using var requestScope = hosting.IsEnabled(LogLevel.Critical)
                ? hosting.BeginScope(new Dictionary<string, string> { ["RequestPath"] = rawPath })
                : null;
            hosting.LogCritical("Hosting diagnostic {RequestPath}", rawPath);
            factory.CreateLogger("Microsoft.AspNetCore.Server.Kestrel.BadRequests").LogError("Bad request {RequestPath}", rawPath);
            factory.CreateLogger("Microsoft.AspNetCore.Mvc.ModelBinding").LogDebug("Route data {RequestPath}", rawPath);
            var app = factory.CreateLogger("CafeHub.WebApi.Tenancy.TenantResolutionMiddleware");
            app.LogInformation("Below the provider's minimum.");
            app.LogWarning("Tenant resolution rejected the request.");
            app.LogError("Request failed with InvalidOperationException.");

            Assert.False(hosting.IsEnabled(LogLevel.Critical));
            Assert.Equal(new[] { "Tenant resolution rejected the request.", "Request failed with InvalidOperationException." },
                provider.Entries);
        }

        [Fact]
        public void ProtectTableQrLogs_AlsoAppliesWhenTheMinimumLevelHasNoMatchingRule()
        {
            var provider = new ScopedLogProvider();
            using var factory = LoggerFactory.Create(logging =>
            {
                logging.AddProvider(provider);
                logging.SetMinimumLevel(LogLevel.Trace);
                logging.ProtectTableQrLogs();
            });

            Assert.False(factory.CreateLogger("Microsoft.AspNetCore.Hosting.Diagnostics").IsEnabled(LogLevel.Critical));
            Assert.False(factory.CreateLogger("Microsoft.AspNetCore.Routing.EndpointRoutingMiddleware").IsEnabled(LogLevel.Debug));
            Assert.True(factory.CreateLogger("Microsoft.AspNetCore.Routing.EndpointRoutingMiddleware").IsEnabled(LogLevel.Warning));
            Assert.True(factory.CreateLogger("CafeHub.WebApi").IsEnabled(LogLevel.Trace));
        }

        private sealed class ScopedLogProvider : ILoggerProvider, ISupportExternalScope
        {
            private IExternalScopeProvider scopes = new LoggerExternalScopeProvider();

            public List<string> Entries { get; } = [];

            public ILogger CreateLogger(string categoryName) => new ScopedLogger(this);

            public void SetScopeProvider(IExternalScopeProvider scopeProvider) => scopes = scopeProvider;

            public void Dispose()
            {
            }

            private sealed class ScopedLogger(ScopedLogProvider provider) : ILogger
            {
                public IDisposable? BeginScope<TState>(TState state) where TState : notnull => provider.scopes.Push(state);

                public bool IsEnabled(LogLevel logLevel) => true;

                public void Log<TState>(
                    LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                {
                    var scopes = new List<string>();
                    provider.scopes.ForEachScope((scope, entries) =>
                    {
                        if (scope is IEnumerable<KeyValuePair<string, string>> values)
                        {
                            entries.AddRange(values.Select(value => value.Value));
                        }
                    }, scopes);
                    provider.Entries.Add(string.Join(" ", scopes.Append(formatter(state, exception))));
                }
            }
        }
    }
}
