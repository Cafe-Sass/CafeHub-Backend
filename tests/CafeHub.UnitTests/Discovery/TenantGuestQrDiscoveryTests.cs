using System.Net;
using System.Reflection;
using System.Text.Json;
using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Discovery;
using CafeHub.Application.Discovery.Queries;
using CafeHub.Application.Tables;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Controllers;
using CafeHub.WebApi.RateLimiting;
using CafeHub.WebApi.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CafeHub.UnitTests.Discovery
{
    public sealed class TenantGuestQrDiscoveryTests
    {
        private static readonly Guid LatteId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private const string Code = "Abcdefghijklmnopqrstuv";
        private const string Text = "something cold";

        [Fact]
        public void Discovery_IsAnAnonymousAllowListedGuestQrPostWithTheStricterPolicy()
        {
            var action = Action();

            Assert.Equal("{code}/discovery", action.GetCustomAttribute<HttpPostAttribute>()?.Template);
            Assert.NotNull(action.GetCustomAttribute<AllowAnonymousAttribute>());
            Assert.True(action.GetCustomAttribute<TableQrEndpointAttribute>()!.IsGuest);
            Assert.Equal(
                DiscoveryRateLimiting.PolicyName,
                action.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName);
            Assert.Null(action.GetCustomAttribute<TenantAuthorizeAttribute>());
        }

        [Fact]
        public async Task Discovery_AnAiResponseCarriesTheSourcePricesAndTheProviderReason()
        {
            var discovery = new ProductDiscoveryResult(
                ProductDiscoverySources.Ai,
                [
                    new ProductDiscoveryItem(LatteId, "Latte", 25.50m, 32m, "Cold and milky"),
                ]);
            var sender = new RecordingSender { Response = Result.Success(discovery) };
            var controller = CreateController(sender, new DefaultHttpContext());

            var result = await controller.Discovery("test-cafe", Code, new ProductDiscoveryRequest(Text), default);

            var body = Assert.IsType<ProductDiscoveryResult>(Assert.IsType<OkObjectResult>(result.Result).Value);
            Assert.Equal(ProductDiscoverySources.Ai, body.Source);
            var item = Assert.Single(body.Items);
            Assert.Equal(LatteId, item.Id);
            Assert.Equal("Latte", item.Name);
            Assert.Equal(25.50m, item.PriceFrom);
            Assert.Equal(32m, item.PriceTo);
            Assert.Equal("Cold and milky", item.Reason);
            Assert.Equal(
                "{\"source\":\"ai\",\"items\":[{\"id\":\"11111111-1111-1111-1111-111111111111\",\"name\":\"Latte\",\"priceFrom\":25.50,\"priceTo\":32,\"reason\":\"Cold and milky\"}]}",
                JsonSerializer.Serialize(body, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            var query = Assert.IsType<GetProductDiscoveryQuery>(sender.LastRequest);
            Assert.Equal(Code, query.Code);
            Assert.Equal(Text, query.Query);
        }

        [Fact]
        public async Task Discovery_ASearchResponseCarriesNoReason()
        {
            var discovery = new ProductDiscoveryResult(
                ProductDiscoverySources.Search,
                [new ProductDiscoveryItem(LatteId, "Latte", null, null, null)]);
            var sender = new RecordingSender { Response = Result.Success(discovery) };
            var controller = CreateController(sender, new DefaultHttpContext());

            var result = await controller.Discovery("test-cafe", Code, new ProductDiscoveryRequest(Text), default);

            var body = Assert.IsType<ProductDiscoveryResult>(Assert.IsType<OkObjectResult>(result.Result).Value);
            Assert.Equal(ProductDiscoverySources.Search, body.Source);
            var item = Assert.Single(body.Items);
            Assert.Null(item.Reason);
            Assert.Null(item.PriceFrom);
            Assert.Null(item.PriceTo);
            Assert.Contains("\"source\":\"search\"", JsonSerializer.Serialize(
                body, new JsonSerializerOptions(JsonSerializerDefaults.Web)), StringComparison.Ordinal);
        }

        [Fact]
        public async Task Discovery_AnInvalidCodeReturnsTheUniformNotFoundWithoutTheText()
        {
            var context = new DefaultHttpContext { TraceIdentifier = "trace-discovery" };
            context.Response.Body = new MemoryStream();
            var sender = new RecordingSender
            {
                Response = Result.Failure<ProductDiscoveryResult>(GuestQrErrors.NotFound()),
            };
            var controller = CreateController(sender, context);

            var result = await controller.Discovery("test-cafe", Code, new ProductDiscoveryRequest(Text), default);

            Assert.IsType<EmptyResult>(result.Result);
            Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
            Assert.Equal("application/problem+json", context.Response.ContentType);
            context.Response.Body.Position = 0;
            using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
            var body = reader.ReadToEnd();
            Assert.Contains("GuestQr.NotFound", body);
            Assert.DoesNotContain(Text, body);
            Assert.DoesNotContain(Code, body);
        }

        [Fact]
        public async Task Discovery_AValidationFailureIsAProblemResponse()
        {
            var context = new DefaultHttpContext { TraceIdentifier = "trace-discovery" };
            context.Response.Body = new MemoryStream();
            var sender = new RecordingSender
            {
                Response = Result.Failure<ProductDiscoveryResult>(ValidationError.Create(
                    [new FieldError("query", "The query is required.")])),
            };
            var controller = CreateController(sender, context);

            var result = await controller.Discovery("test-cafe", Code, new ProductDiscoveryRequest(null), default);

            var problem = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        }

        [Theory]
        [InlineData(300, 150)]
        [InlineData(101, 50)]
        [InlineData(3, 1)]
        [InlineData(1, 1)]
        public void ResolvePermitLimit_AdmitsHalfTheGlobalLimitAndNeverLessThanOne(
            int globalPermitLimit,
            int expected)
        {
            var options = new GlobalRateLimitOptions
            {
                PermitLimit = globalPermitLimit,
                WindowSeconds = 60,
            };

            Assert.Equal(expected, DiscoveryRateLimiting.ResolvePermitLimit(options));
        }

        [Fact]
        public void CreatePartition_AdmitsOnlyHalfOfTheGlobalPermitLimit()
        {
            var options = new GlobalRateLimitOptions { PermitLimit = 4, WindowSeconds = 60 };
            var context = new DefaultHttpContext
            {
                Connection = { RemoteIpAddress = IPAddress.Parse("10.1.2.3") },
            };

            var partition = DiscoveryRateLimiting.CreatePartition(context, options);
            var limiter = partition.Factory(partition.PartitionKey);

            Assert.Equal("10.1.2.3", partition.PartitionKey);
            Assert.True(limiter.AttemptAcquire().IsAcquired);
            Assert.True(limiter.AttemptAcquire().IsAcquired);
            Assert.False(limiter.AttemptAcquire().IsAcquired);
        }

        private static MethodInfo Action() =>
            typeof(TenantGuestQrController).GetMethod(nameof(TenantGuestQrController.Discovery))!;

        private static TenantGuestQrController CreateController(ISender sender, HttpContext context) =>
            new(sender, new FixedCorrelationIdAccessor(null))
            {
                ControllerContext = new ControllerContext { HttpContext = context },
            };

        private sealed class FixedCorrelationIdAccessor(string? correlationId) : ICorrelationIdAccessor
        {
            public string? CorrelationId => correlationId;
        }

        private sealed class RecordingSender : ISender
        {
            public object? Response { get; init; }

            public object? LastRequest { get; private set; }

            public Task<TResponse> Send<TResponse>(
                IRequest<TResponse> request,
                CancellationToken cancellationToken = default)
            {
                LastRequest = request;
                return Task.FromResult((TResponse)Response!);
            }

            public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
                where TRequest : IRequest =>
                throw new NotSupportedException("The controller must send a request with a response.");

            public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException("The controller must send a typed request.");

            public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
                IStreamRequest<TResponse> request,
                CancellationToken cancellationToken = default) =>
                throw new NotSupportedException("The controller does not create streams.");

            public IAsyncEnumerable<object?> CreateStream(
                object request,
                CancellationToken cancellationToken = default) =>
                throw new NotSupportedException("The controller does not create streams.");
        }
    }
}
