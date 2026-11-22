using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.Json;
using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Menu.DTOs;
using CafeHub.Application.Menu.Queries;
using CafeHub.Application.Pagination;
using CafeHub.Application.Tables;
using CafeHub.Application.Tables.DTOs;
using CafeHub.Application.Tables.Queries;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Tenancy;
using CafeHub.WebApi;
using CafeHub.WebApi.Controllers;
using CafeHub.WebApi.Cors;
using CafeHub.WebApi.Errors;
using CafeHub.WebApi.RateLimiting;
using CafeHub.WebApi.Security;
using CafeHub.WebApi.Tenancy;
using CafeHub.WebApi.Transport;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CafeHub.UnitTests.Tables
{
    public sealed class TenantGuestQrControllerTests
    {
        private const string Code = "Abcdefghijklmnopqrstuv";

        [Fact]
        public async Task Resolve_IsExplicitlyAnonymous_AndReturnsOnlyTheGuestContext()
        {
            var action = typeof(TenantGuestQrController).GetMethod(nameof(TenantGuestQrController.Resolve))!;
            Assert.NotNull(action.GetCustomAttribute<AllowAnonymousAttribute>());
            Assert.Equal("{**code}", action.GetCustomAttribute<HttpGetAttribute>()?.Template);
            var context = new GuestQrContext("Cafe", Guid.NewGuid(), "Downtown", Guid.NewGuid(), 5);
            var sender = new RecordingSender { Response = Result.Success(context) };
            var controller = new TenantGuestQrController(sender, new FixedCorrelationIdAccessor(null));

            var result = await controller.Resolve("test-cafe", Code, default);

            Assert.Same(context, Assert.IsType<OkObjectResult>(result.Result).Value);
            Assert.Equal(Code, Assert.IsType<ResolveGuestQrQuery>(sender.LastRequest).Code);
        }

        [Fact]
        public async Task Menu_IsExplicitlyAnonymousAndSendsTheRouteCode()
        {
            var action = typeof(TenantGuestQrController).GetMethod(nameof(TenantGuestQrController.Menu))!;
            Assert.NotNull(action.GetCustomAttribute<AllowAnonymousAttribute>());
            Assert.Equal("{code}/menu", action.GetCustomAttribute<HttpGetAttribute>()?.Template);
            var page = OffsetPage<GuestMenuItemResponse>.Create([], 1, 20, null);
            var sender = new RecordingSender { Response = Result.Success(page) };
            var controller = new TenantGuestQrController(sender, new FixedCorrelationIdAccessor(null));

            var result = await controller.Menu("test-cafe", Code, new GetGuestMenuQuery { Search = "latte" }, default);

            Assert.Same(page, Assert.IsType<OkObjectResult>(result.Result).Value);
            var sent = Assert.IsType<GetGuestMenuQuery>(sender.LastRequest);
            Assert.Equal(Code, sent.Code);
            Assert.Equal("latte", sent.Search);
        }

        [Fact]
        public async Task Menu_InvalidCodeReturnsTheSameNotFoundBodyAsTheResolveEndpoint()
        {
            var context = CreateContext();
            var controller = new TenantGuestQrController(
                new RecordingSender
                {
                    Response = Result.Failure<OffsetPage<GuestMenuItemResponse>>(GuestQrErrors.NotFound()),
                },
                new FixedCorrelationIdAccessor("correlation-menu-test"))
            {
                ControllerContext = new ControllerContext { HttpContext = context },
            };

            var result = await controller.Menu("test-cafe", Code, new GetGuestMenuQuery(), default);

            Assert.IsType<EmptyResult>(result.Result);
            Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
            Assert.Equal("application/problem+json", context.Response.ContentType);
            var body = ReadBody(context);
            Assert.DoesNotContain(Code, body);
            using var problem = JsonDocument.Parse(body);
            Assert.Equal("GuestQr.NotFound", problem.RootElement.GetProperty("code").GetString());
        }

        [Fact]
        public async Task Menu_InvalidFilterReturnsAValidationProblem()
        {
            var context = CreateContext();
            var controller = new TenantGuestQrController(
                new RecordingSender
                {
                    Response = Result.Failure<OffsetPage<GuestMenuItemResponse>>(
                        PaginationErrors.SortFieldIsNotAllowed("bestselling")),
                },
                new FixedCorrelationIdAccessor("correlation-menu-test"))
            {
                ControllerContext = new ControllerContext { HttpContext = context },
            };

            var result = await controller.Menu("test-cafe", Code, new GetGuestMenuQuery { Sort = "bestselling" }, default);

            var problem = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        }

        [Theory]
        [InlineData("test-cafe", TenantLifecycleStatus.Suspended)]
        [InlineData("test-cafe", TenantLifecycleStatus.Terminated)]
        [InlineData("test-cafe", TenantLifecycleStatus.Provisioning)]
        [InlineData("test-cafe", null)]
        [InlineData("bad.slug", null)]
        public async Task UnavailableTenantAndRejectedCode_UseTheSameNotFoundResponse(string slug, TenantLifecycleStatus? state)
        {
            var routeContext = CreateContext(slug);
            var actionContext = CreateContext();
            var correlation = new FixedCorrelationIdAccessor("correlation-qr-test");
            var logger = new FakeLogger<TenantResolutionMiddleware>();
            var middleware = new TenantResolutionMiddleware(_ => throw new InvalidOperationException("Rejected context reached MVC."));
            var tenantResolver = new StubTenantResolver(state);
            var sender = new RecordingSender { Response = Result.Failure<GuestQrContext>(GuestQrErrors.NotFound()) };
            var controller = new TenantGuestQrController(sender, correlation)
            {
                ControllerContext = new ControllerContext { HttpContext = actionContext },
            };

            await middleware.InvokeAsync(routeContext, tenantResolver, new TenantStateGuard(), new TenantContextAccessor(), logger, correlation);
            await controller.Resolve("test-cafe", Code, default);

            Assert.Equal(StatusCodes.Status404NotFound, actionContext.Response.StatusCode);
            Assert.Equal(actionContext.Response.StatusCode, routeContext.Response.StatusCode);
            Assert.Equal("application/problem+json", routeContext.Response.ContentType);
            Assert.Equal(actionContext.Response.ContentType, routeContext.Response.ContentType);
            var body = ReadBody(routeContext);
            Assert.Equal(ReadBody(actionContext), body);
            Assert.DoesNotContain(Code, body);
            using var problem = JsonDocument.Parse(body);
            Assert.Equal("GuestQr.NotFound", problem.RootElement.GetProperty("code").GetString());
            Assert.DoesNotContain(Code, Assert.Single(logger.Entries).Message);
        }

        [Fact]
        public async Task ExceptionHandler_SuppressesExceptionDetailsAndUsesTheGuestNotFoundResponse()
        {
            var context = CreateContext();
            var logger = new FakeLogger<GlobalExceptionHandler>();
            var handler = new GlobalExceptionHandler(logger);

            await handler.TryHandleAsync(context, new InvalidTenantContextException(Code), default);

            Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
            var body = ReadBody(context);
            Assert.Contains("GuestQr.NotFound", body);
            Assert.DoesNotContain(Code, body);
            var entry = Assert.Single(logger.Entries);
            Assert.Null(entry.Exception);
            Assert.DoesNotContain(Code, entry.Message);
        }

        [Fact]
        public async Task MalformedPaths_MatchTheGuestRouteAndShareTheController404WithoutTenantLookup()
        {
            var resolver = new StubTenantResolver(TenantLifecycleStatus.Active);
            var sender = new RecordingSender { Response = Result.Failure<GuestQrContext>(GuestQrErrors.NotFound()) };
            await using var app = CreateApp(resolver, sender, permitLimit: 10);
            var pipeline = ((IApplicationBuilder)app).Build();
            const string route = "/t/test-cafe/api/v1/guest/qr";
            var unknownCode = await SendAsync(app, pipeline, $"{route}/{Code}");
            Assert.Equal(1, resolver.CallCount);
            Assert.Equal(1, sender.CallCount);

            foreach (var path in new[] { route, $"{route}/", $"{route}/{Code}/extra", $"{route}/short" })
            {
                var malformed = await SendAsync(app, pipeline, path);

                Assert.Equal(StatusCodes.Status404NotFound, malformed.Response.StatusCode);
                Assert.Equal(unknownCode.Response.ContentType, malformed.Response.ContentType);
                Assert.Equal(ReadBody(unknownCode), ReadBody(malformed));
                Assert.Equal("no-store", malformed.Response.Headers.CacheControl.ToString());
                Assert.Equal(1, resolver.CallCount);
                Assert.Equal(1, sender.CallCount);
            }
        }

        [Fact]
        public async Task MenuPath_RoutesToTheMenuActionAndSharesTheGuestNotFoundContract()
        {
            var resolver = new StubTenantResolver(TenantLifecycleStatus.Active);
            var sender = new RecordingSender
            {
                Response = Result.Failure<OffsetPage<GuestMenuItemResponse>>(GuestQrErrors.NotFound()),
            };
            await using var app = CreateApp(resolver, sender, permitLimit: 10);
            var pipeline = ((IApplicationBuilder)app).Build();

            var response = await SendAsync(app, pipeline, $"/t/test-cafe/api/v1/guest/qr/{Code}/menu");

            Assert.Equal(StatusCodes.Status404NotFound, response.Response.StatusCode);
            Assert.Equal(1, sender.CallCount);
            Assert.IsType<GetGuestMenuQuery>(sender.LastRequest);
            Assert.Equal("no-store", response.Response.Headers.CacheControl.ToString());
            Assert.DoesNotContain(Code, ReadBody(response));
        }

        [Theory]
        [InlineData("https://public.example.test", true)]
        [InlineData("https://unlisted.example.test", false)]
        public async Task CorsAndRateLimiter_RateLimitMalformedScansBeforeTenantLookupAndExposeTheAllowedResponse(
            string origin, bool allowed)
        {
            var resolver = new StubTenantResolver(null);
            var sender = new RecordingSender();
            await using var app = CreateApp(resolver, sender, permitLimit: 1);
            var pipeline = ((IApplicationBuilder)app).Build();
            const string route = "/t/test-cafe/api/v1/guest/qr";

            var first = await SendAsync(app, pipeline, route, origin);
            var rejected = await SendAsync(app, pipeline, $"{route}/{Code}/extra", origin);

            Assert.Equal(StatusCodes.Status404NotFound, first.Response.StatusCode);
            Assert.Equal(StatusCodes.Status429TooManyRequests, rejected.Response.StatusCode);
            Assert.True(int.TryParse(rejected.Response.Headers["Retry-After"], out var seconds));
            Assert.True(seconds > 0);
            Assert.Equal("no-store", rejected.Response.Headers.CacheControl.ToString());
            Assert.DoesNotContain(Code, ReadBody(rejected));
            Assert.Equal(0, resolver.CallCount);
            Assert.Equal(0, sender.CallCount);
            if (allowed)
            {
                Assert.Equal(origin, rejected.Response.Headers.AccessControlAllowOrigin.ToString());
                Assert.Contains("Retry-After", rejected.Response.Headers.AccessControlExposeHeaders.ToString());
            }
            else
            {
                Assert.False(rejected.Response.Headers.ContainsKey("Access-Control-Allow-Origin"));
                Assert.False(rejected.Response.Headers.ContainsKey("Access-Control-Expose-Headers"));
            }
        }

        private static WebApplication CreateApp(StubTenantResolver resolver, RecordingSender sender, int permitLimit)
        {
            var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Development,
            });
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [GlobalRateLimitConfigurationKeys.PermitLimitPath] = permitLimit.ToString(CultureInfo.InvariantCulture),
                [GlobalRateLimitConfigurationKeys.WindowSecondsPath] = "60",
                [$"{CorsConfigurationKeys.AllowedOriginsPath}:0"] = "https://public.example.test",
                [$"{CorsConfigurationKeys.ExposedHeadersPath}:0"] = "X-Correlation-Id",
            });
            builder.Services.AddLogging();
            builder.Services.AddMetrics();
            builder.Services.AddSingleton<IServer, NoNetworkServer>();
            builder.Services.AddControllers().AddApplicationPart(typeof(TenantGuestQrController).Assembly);
            builder.Services.AddAuthentication();
            builder.Services.AddAuthorization();
            builder.Services.AddProblemDetails();
            builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
            builder.Services.AddTransportSecurity(builder.Configuration);
            builder.Services.AddConfiguredCors(builder.Configuration);
            builder.Services.AddGlobalRateLimiting(builder.Configuration);
            builder.Services.AddSingleton<ITenantResolver>(resolver);
            builder.Services.AddSingleton<ITenantStateGuard, TenantStateGuard>();
            builder.Services.AddScoped<ITenantContextSetter, TenantContextAccessor>();
            builder.Services.AddSingleton<ICorrelationIdAccessor>(new FixedCorrelationIdAccessor(null));
            builder.Services.AddSingleton<ISender>(sender);

            var app = builder.Build();
            app.ConfigureTenantApi();
            ((IApplicationBuilder)app).UseEndpoints(_ => { });
            return app;
        }

        private static async Task<DefaultHttpContext> SendAsync(
            WebApplication app, RequestDelegate pipeline, string path, string? origin = null)
        {
            using var scope = app.Services.CreateScope();
            var context = new DefaultHttpContext { TraceIdentifier = "trace-qr-test", RequestServices = scope.ServiceProvider };
            context.Connection.RemoteIpAddress = IPAddress.Loopback;
            context.Request.Method = HttpMethods.Get;
            context.Request.Scheme = "https";
            context.Request.Path = path;
            if (origin is not null)
            {
                context.Request.Headers.Origin = origin;
            }

            var response = new StartingResponseFeature();
            context.Features.Set<IHttpResponseFeature>(response);
            context.Response.Body = new MemoryStream();
            await pipeline(context);
            await response.RunOnStartingAsync();
            var endpoint = Assert.IsType<RouteEndpoint>(context.GetEndpoint());
            var policy = Assert.Single(endpoint.Metadata.GetOrderedMetadata<EnableRateLimitingAttribute>());
            Assert.Equal(GlobalRateLimiting.PolicyName, policy.PolicyName);
            return context;
        }

        private static DefaultHttpContext CreateContext(string slug = "test-cafe")
        {
            var context = new DefaultHttpContext { TraceIdentifier = "trace-qr-test" };
            context.Request.Method = HttpMethods.Get;
            context.Request.Path = $"/t/{slug}/api/v1/guest/qr/{Code}";
            context.Request.RouteValues["code"] = Code;
            context.Response.Body = new MemoryStream();
            var action = typeof(TenantGuestQrController).GetMethod(nameof(TenantGuestQrController.Resolve))!;
            context.SetEndpoint(new Endpoint(
                _ => Task.CompletedTask,
                new EndpointMetadataCollection(action.GetCustomAttributes()),
                nameof(TenantGuestQrController)));
            return context;
        }

        private static string ReadBody(HttpContext context)
        {
            context.Response.Body.Position = 0;
            using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
            return reader.ReadToEnd();
        }

        private sealed class StubTenantResolver(TenantLifecycleStatus? state) : ITenantResolver
        {
            public int CallCount { get; private set; }

            public Task<Result<ResolvedTenant>> ResolveAsync(string rawSlug, CancellationToken cancellationToken)
            {
                CallCount++;
                return Task.FromResult(state is { } lifecycle
                    ? Result.Success(new ResolvedTenant(Guid.NewGuid(), rawSlug, lifecycle, "test-reference"))
                    : Result.Failure<ResolvedTenant>(TenancyErrors.TenantUnavailable()));
            }
        }

        private sealed class NoNetworkServer : IServer
        {
            public IFeatureCollection Features { get; } = new FeatureCollection();

            public Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken cancellationToken)
                where TContext : notnull => throw new NotSupportedException();

            public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

            public void Dispose()
            {
            }
        }

        private sealed class StartingResponseFeature : HttpResponseFeature
        {
            private readonly Stack<(Func<object, Task> Callback, object State)> callbacks = new();

            public override void OnStarting(Func<object, Task> callback, object state) =>
                callbacks.Push((callback, state));

            public async Task RunOnStartingAsync()
            {
                foreach (var (callback, state) in callbacks)
                {
                    await callback(state);
                }
            }
        }
    }
}
