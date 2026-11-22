using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Orders;
using CafeHub.Application.Tables;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Tenancy;
using CafeHub.WebApi;
using CafeHub.WebApi.Controllers;
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

namespace CafeHub.UnitTests.Orders
{
    public sealed class TenantGuestOrdersControllerTests
    {
        private const string Code = "Abcdefghijklmnopqrstuv";
        private static readonly Guid ProductId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid SizeId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly SubmitOrderResult OrderResult = new(
            7, "OrderPublicReference22", "Pending", 42.50m, DateTimeOffset.Parse("2026-11-22T10:05:00Z"));

        [Fact]
        public void Submit_IsAnAnonymousGuestPostActionOnTheOrdersRoute()
        {
            var action = typeof(TenantGuestOrdersController).GetMethod(nameof(TenantGuestOrdersController.Submit))!;

            Assert.NotNull(action.GetCustomAttribute<HttpPostAttribute>());
            Assert.NotNull(action.GetCustomAttribute<AllowAnonymousAttribute>());
            Assert.True(action.GetCustomAttribute<TableQrEndpointAttribute>()!.IsGuest);
            Assert.Equal(
                "t/{slug}/api/v1/guest/orders",
                typeof(TenantGuestOrdersController).GetCustomAttribute<RouteAttribute>()!.Template);
        }

        [Fact]
        public async Task Submit_MapsTheBodyHeadersAndGuestSourceToTheCommand()
        {
            var sender = new RecordingSender { Response = Result.Success(OrderResult) };
            var controller = new TenantGuestOrdersController(sender, new FixedCorrelationIdAccessor(null))
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
            };

            await controller.Submit(
                "test-cafe",
                CreateRequest(),
                "guest-order-key-1",
                "Abcdefghijklmnop1234",
                default);

            var command = Assert.IsType<SubmitOrderCommand>(sender.LastRequest);
            Assert.Equal("guest-order-key-1", command.IdempotencyKey);
            Assert.Equal(Code, command.Code);
            Assert.Equal(OrderServiceType.DineIn, command.ServiceType);
            Assert.Equal(OrderSource.Guest, command.Source);
            Assert.Null(command.ActingStaffUserId);
            Assert.Equal("Sara", command.GuestDisplayName);
            Assert.Equal("Abcdefghijklmnop1234", command.GuestDeviceId);
            Assert.Equal(42.50m, command.ExpectedTotal!.Value);
            var line = Assert.Single(command.Lines);
            Assert.Equal(ProductId, line.ProductId);
            Assert.Equal(SizeId, line.SizeId);
            Assert.Empty(line.AddonIds);
            Assert.Equal(2, line.Quantity);
            Assert.Equal(15.00m, line.ExpectedUnitPrice);
            Assert.Empty(line.ExpectedAddonPrices);
        }

        [Theory]
        [InlineData("Abcdefghijklmnop1234", "Abcdefghijklmnop1234")]
        [InlineData("short", null)]
        [InlineData("device id with spaces", null)]
        [InlineData("device!with!symbols!and!len", null)]
        [InlineData(null, null)]
        public async Task Submit_KeepsOnlyAWellFormedGuestDeviceId(string? header, string? expected)
        {
            var sender = new RecordingSender { Response = Result.Success(OrderResult) };
            var controller = new TenantGuestOrdersController(sender, new FixedCorrelationIdAccessor(null))
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
            };

            await controller.Submit("test-cafe", CreateRequest(), "guest-order-key-1", header, default);

            Assert.Equal(expected, Assert.IsType<SubmitOrderCommand>(sender.LastRequest).GuestDeviceId);
        }

        [Fact]
        public async Task Submit_AnUnknownServiceTypeReturnsAValidationProblemWithoutCallingTheUseCase()
        {
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();
            var sender = new RecordingSender { Response = Result.Success(OrderResult) };
            var controller = new TenantGuestOrdersController(sender, new FixedCorrelationIdAccessor(null))
            {
                ControllerContext = new ControllerContext { HttpContext = context },
            };
            var request = CreateRequest() with { ServiceType = "Delivery" };

            var result = await controller.Submit("test-cafe", request, "guest-order-key-1", null, default);

            var problem = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
            var details = Assert.IsType<ProblemDetails>(problem.Value);
            Assert.Equal(OrdersErrors.ServiceTypeInvalidCode, details.Extensions["code"]);
            Assert.Equal(0, sender.CallCount);
        }

        [Fact]
        public async Task Submit_ARejectedQrWritesTheUniformGuestNotFoundBody()
        {
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();
            var controller = new TenantGuestOrdersController(
                new RecordingSender { Response = Result.Failure<SubmitOrderResult>(GuestQrErrors.NotFound()) },
                new FixedCorrelationIdAccessor("correlation-order-test"))
            {
                ControllerContext = new ControllerContext { HttpContext = context },
            };

            var result = await controller.Submit("test-cafe", CreateRequest(), "guest-order-key-1", null, default);

            Assert.IsType<EmptyResult>(result.Result);
            Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
            Assert.Equal("application/problem+json", context.Response.ContentType);
            var body = ReadBody(context);
            Assert.DoesNotContain(Code, body);
            using var problem = JsonDocument.Parse(body);
            Assert.Equal(GuestQrErrors.NotFoundCode, problem.RootElement.GetProperty("code").GetString());
        }

        [Fact]
        public async Task Submit_AChangedPriceReturnsConflictWithTheChangedItems()
        {
            var changes = new[]
            {
                new OrderPriceChange(
                    1, ProductId, SizeId, 16.00m, [new OrderAddonPrice(Guid.NewGuid(), "Milk", 2.00m)], 36.00m),
            };
            var problem = await SubmitFailureAsync(OrdersErrors.PriceChanged(changes, 36.00m));

            Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
            var details = Assert.IsType<ProblemDetails>(problem.Value);
            Assert.Equal(OrdersErrors.PriceChangedCode, details.Extensions["code"]);
            Assert.Same(changes, details.Extensions[OrdersErrors.ItemsExtension]);
            Assert.Equal(36.00m, Assert.IsType<decimal>(details.Extensions[OrdersErrors.TotalExtension]));
        }

        [Fact]
        public async Task Submit_UnavailableProductsReturnConflictWithTheItemIds()
        {
            var items = new[] { new OrderUnavailableItem(1, ProductId) };
            var problem = await SubmitFailureAsync(OrdersErrors.ProductUnavailable(items));

            Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
            var details = Assert.IsType<ProblemDetails>(problem.Value);
            Assert.Equal(OrdersErrors.ProductUnavailableCode, details.Extensions["code"]);
            Assert.Same(items, details.Extensions[OrdersErrors.ItemsExtension]);
        }

        [Fact]
        public async Task Submit_AStoredOrderReturnsCreatedWithTheGuestStatusLocation()
        {
            var sender = new RecordingSender { Response = Result.Success(OrderResult) };
            var controller = new TenantGuestOrdersController(sender, new FixedCorrelationIdAccessor(null))
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
            };

            var result = await controller.Submit("test-cafe", CreateRequest(), "guest-order-key-1", null, default);

            var created = Assert.IsType<CreatedResult>(result.Result);
            Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
            Assert.Equal(
                $"/t/test-cafe/api/v1/guest/orders/{OrderResult.PublicReference}",
                created.Location);
            var response = Assert.IsType<GuestOrderResponse>(created.Value);
            Assert.Equal(OrderResult.OrderNumber, response.OrderNumber);
            Assert.Equal(OrderResult.PublicReference, response.PublicReference);
            Assert.Equal(OrderResult.Status, response.Status);
            Assert.Equal(OrderResult.TotalAmount, response.TotalAmount);
            Assert.Equal(OrderResult.ResponseDeadline, response.ResponseDeadline);
        }

        [Fact]
        public async Task PostRoute_IsGloballyRateLimitedAndReachesTheGuestUseCaseWithoutAuthentication()
        {
            var resolver = new StubTenantResolver(TenantLifecycleStatus.Active);
            var sender = new RecordingSender { Response = Result.Success(OrderResult) };
            await using var app = CreateApp(resolver, sender);
            var pipeline = ((IApplicationBuilder)app).Build();
            var body = JsonSerializer.Serialize(new
            {
                code = Code,
                serviceType = "Takeaway",
                guestDisplayName = "Sara",
                expectedTotal = 42.50m,
                lines = new[]
                {
                    new
                    {
                        productId = ProductId,
                        sizeId = SizeId,
                        addonIds = Array.Empty<Guid>(),
                        quantity = 2,
                        expectedUnitPrice = 15.00m,
                        expectedAddonPrices = Array.Empty<decimal>(),
                    },
                },
            });

            var context = await SendAsync(app, pipeline, body);

            Assert.Equal(StatusCodes.Status201Created, context.Response.StatusCode);
            Assert.Equal(
                $"/t/test-cafe/api/v1/guest/orders/{OrderResult.PublicReference}",
                context.Response.Headers["Location"].ToString());
            var command = Assert.IsType<SubmitOrderCommand>(sender.LastRequest);
            Assert.Equal(Code, command.Code);
            Assert.Equal(OrderServiceType.Takeaway, command.ServiceType);
            Assert.Equal("guest-order-key-1", command.IdempotencyKey);
            Assert.Equal("Abcdefghijklmnop1234", command.GuestDeviceId);
            Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
            Assert.DoesNotContain(Code, ReadBody(context));
        }

        [Fact]
        public async Task PostRoute_AnUnusableTenantUsesTheUniformGuestNotFoundBody()
        {
            var resolver = new StubTenantResolver(null);
            var sender = new RecordingSender { Response = Result.Success(OrderResult) };
            await using var app = CreateApp(resolver, sender);
            var pipeline = ((IApplicationBuilder)app).Build();

            var context = await SendAsync(app, pipeline, "{}");

            Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
            Assert.Equal(0, sender.CallCount);
            using var problem = JsonDocument.Parse(ReadBody(context));
            Assert.Equal(GuestQrErrors.NotFoundCode, problem.RootElement.GetProperty("code").GetString());
        }

        private static async Task<ObjectResult> SubmitFailureAsync(Error error)
        {
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();
            var controller = new TenantGuestOrdersController(
                new RecordingSender { Response = Result.Failure<SubmitOrderResult>(error) },
                new FixedCorrelationIdAccessor(null))
            {
                ControllerContext = new ControllerContext { HttpContext = context },
            };

            var result = await controller.Submit("test-cafe", CreateRequest(), "guest-order-key-1", null, default);

            return Assert.IsType<ObjectResult>(result.Result);
        }

        private static SubmitGuestOrderRequest CreateRequest() =>
            new(
                Code,
                "DineIn",
                "Sara",
                42.50m,
                [new SubmitGuestOrderLineRequest(ProductId, SizeId, [], 2, 15.00m, [])]);

        private static WebApplication CreateApp(StubTenantResolver resolver, RecordingSender sender)
        {
            var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Development,
            });
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [GlobalRateLimitConfigurationKeys.PermitLimitPath] = "10",
                [GlobalRateLimitConfigurationKeys.WindowSecondsPath] = "60",
            });
            builder.Services.AddLogging();
            builder.Services.AddMetrics();
            builder.Services.AddSingleton<IServer, NoNetworkServer>();
            builder.Services.AddControllers().AddApplicationPart(typeof(TenantGuestOrdersController).Assembly);
            builder.Services.AddAuthentication();
            builder.Services.AddAuthorization();
            builder.Services.AddProblemDetails();
            builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
            builder.Services.AddTransportSecurity(builder.Configuration);
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
            WebApplication app, RequestDelegate pipeline, string body)
        {
            using var scope = app.Services.CreateScope();
            var context = new DefaultHttpContext { TraceIdentifier = "trace-order-test", RequestServices = scope.ServiceProvider };
            context.Connection.RemoteIpAddress = IPAddress.Loopback;
            context.Request.Method = HttpMethods.Post;
            context.Request.Scheme = "https";
            context.Request.Path = "/t/test-cafe/api/v1/guest/orders";
            context.Request.ContentType = "application/json";
            context.Request.Headers["Idempotency-Key"] = "guest-order-key-1";
            context.Request.Headers["X-Guest-Device"] = "Abcdefghijklmnop1234";
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
            var response = new StartingResponseFeature();
            context.Features.Set<IHttpResponseFeature>(response);
            context.Response.Body = new MemoryStream();
            await pipeline(context);
            await response.RunOnStartingAsync();
            var endpoint = Assert.IsType<RouteEndpoint>(context.GetEndpoint());
            var policy = Assert.Single(endpoint.Metadata.GetOrderedMetadata<EnableRateLimitingAttribute>());
            Assert.Equal(GlobalRateLimiting.PolicyName, policy.PolicyName);
            Assert.NotNull(endpoint.Metadata.GetMetadata<IAllowAnonymous>());
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
            public Task<Result<ResolvedTenant>> ResolveAsync(string rawSlug, CancellationToken cancellationToken) =>
                Task.FromResult(state is { } lifecycle
                    ? Result.Success(new ResolvedTenant(Guid.NewGuid(), rawSlug, lifecycle, "test-reference"))
                    : Result.Failure<ResolvedTenant>(TenancyErrors.TenantUnavailable()));
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
