using System.Text.Json;
using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Tenants;
using CafeHub.WebApi.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

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

        [Fact]
        public async Task InvokeAsync_MalformedSlug_LogsOnlyCategoryWithoutSlugAndWithoutTenantScope()
        {
            var logger = new RecordingLogger();
            var context = Context("/t/bad.slug");
            var middleware = new TenantResolutionMiddleware(_ => throw new InvalidOperationException("next must not run"));

            await middleware.InvokeAsync(
                context,
                new Resolver(Result.Failure<ResolvedTenant>(TenancyErrors.TenantUnavailable())),
                new TenantStateGuard(),
                new Setter(),
                logger);

            var entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Contains("malformed_slug", entry.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("bad.slug", entry.Message, StringComparison.Ordinal);
            Assert.Equal(0, entry.ActiveScopeCount);
            Assert.Empty(logger.Scopes);
        }

        [Fact]
        public async Task InvokeAsync_UnknownSlug_LogsOnlyUnavailableCategoryWithoutSlugAndWithoutTenantScope()
        {
            var logger = new RecordingLogger();
            var context = Context("/t/unknown");
            var middleware = new TenantResolutionMiddleware(_ => throw new InvalidOperationException("next must not run"));

            await middleware.InvokeAsync(
                context,
                new Resolver(Result.Failure<ResolvedTenant>(TenancyErrors.TenantUnavailable())),
                new TenantStateGuard(),
                new Setter(),
                logger);

            var entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Contains("unavailable", entry.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("unknown", entry.Message, StringComparison.Ordinal);
            Assert.Equal(0, entry.ActiveScopeCount);
            Assert.Empty(logger.Scopes);
        }

        [Fact]
        public async Task InvokeAsync_UnusableTenant_LogsOnlyUnavailableCategoryWithoutSlugAndWithoutTenantScope()
        {
            var logger = new RecordingLogger();
            var context = Context("/t/unusable-cafe/menu");
            var setter = new Setter();
            var middleware = new TenantResolutionMiddleware(_ => throw new InvalidOperationException("next must not run"));

            await middleware.InvokeAsync(
                context,
                new Resolver(Result.Success(new ResolvedTenant(Guid.NewGuid(), "unusable-cafe", TenantLifecycleStatus.Suspended, "ref"))),
                new TenantStateGuard(),
                setter,
                logger);

            Assert.Equal(0, setter.Count);
            var entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Contains("unavailable", entry.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("unusable-cafe", entry.Message, StringComparison.Ordinal);
            Assert.Equal(0, entry.ActiveScopeCount);
            Assert.Empty(logger.Scopes);
        }

        [Fact]
        public async Task InvokeAsync_ResolvedTenant_LogsInsideAnOpenTenantIdScopeThatCarriesNoSlug()
        {
            var logger = new RecordingLogger();
            var id = Guid.NewGuid();
            var resolver = new Resolver(Result.Success(new ResolvedTenant(id, "cafe", TenantLifecycleStatus.Active, "ref")));
            var setter = new Setter();
            var middleware = new TenantResolutionMiddleware(_ =>
            {
                logger.LogInformation("downstream work");
                return Task.CompletedTask;
            });

            await middleware.InvokeAsync(Context("/t/cafe/menu"), resolver, new TenantStateGuard(), setter, logger);

            Assert.Equal(1, setter.Count);
            var entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Information, entry.Level);
            Assert.Equal(1, entry.ActiveScopeCount);
            Assert.DoesNotContain("cafe", entry.Message, StringComparison.Ordinal);

            var scope = Assert.Single(logger.Scopes);
            Assert.True(scope.IsDisposed);
            var values = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(scope.State);
            Assert.Equal(id, values["TenantId"]);
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

        private sealed record RecordedLogEntry(LogLevel Level, string Message, Exception? Exception, int ActiveScopeCount);

        private sealed class RecordingLogger : ILogger<TenantResolutionMiddleware>
        {
            private int activeScopeCount;

            public List<RecordedLogEntry> Entries { get; } = [];

            public List<RecordedScope> Scopes { get; } = [];

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            {
                var scope = new RecordedScope(state, this);
                Scopes.Add(scope);
                activeScopeCount++;
                return scope;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                Entries.Add(new RecordedLogEntry(logLevel, formatter(state, exception), exception, activeScopeCount));
            }

            private void CloseScope()
            {
                activeScopeCount--;
            }

            public sealed class RecordedScope(object? state, RecordingLogger logger) : IDisposable
            {
                public object? State { get; } = state;

                public bool IsDisposed { get; private set; }

                public void Dispose()
                {
                    if (!IsDisposed)
                    {
                        IsDisposed = true;
                        logger.CloseScope();
                    }
                }
            }
        }
    }
}
