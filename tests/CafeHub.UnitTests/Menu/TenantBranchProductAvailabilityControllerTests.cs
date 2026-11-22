using System.Reflection;
using System.Text.Json;
using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Menu;
using CafeHub.Application.Menu.Commands;
using CafeHub.Application.Menu.DTOs;
using CafeHub.Application.Menu.Queries;
using CafeHub.Application.Pagination;
using CafeHub.WebApi.Authentication;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Controllers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace CafeHub.UnitTests.Menu
{
    public sealed class TenantBranchProductAvailabilityControllerTests
    {
        private static readonly Guid BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        private static readonly Guid ProductId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

        [Theory]
        [InlineData(nameof(TenantBranchProductAvailabilityController.Set), "PUT", "{productId:guid}")]
        [InlineData(nameof(TenantBranchProductAvailabilityController.List), "GET", null)]
        public void RoutesDeclareTheirMethodAndTenantPolicy(string action, string verb, string? template)
        {
            Assert.Equal(
                "t/{slug}/api/v1/branches/{branchId:guid}/menu/availability/products",
                typeof(TenantBranchProductAvailabilityController).GetCustomAttribute<RouteAttribute>()!.Template);
            var method = typeof(TenantBranchProductAvailabilityController).GetMethod(action)!;
            var route = Assert.Single(method.GetCustomAttributes<HttpMethodAttribute>());
            Assert.Equal(verb, Assert.Single(route.HttpMethods));
            Assert.Equal(template, route.Template);
            var access = method.GetCustomAttribute<TenantAuthorizeAttribute>()!;
            Assert.Equal(TenantPermissions.MenuAvailability, access.Policy);
            Assert.Equal(TenantAuthenticationSchemes.Bearer, access.AuthenticationSchemes);
        }

        [Fact]
        public void MissingReasonDefaultsToManual()
        {
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

            var request = JsonSerializer.Deserialize<SetBranchProductAvailabilityRequest>(
                """{"available":false}""", options);

            Assert.False(request!.Available);
            Assert.Equal("manual", request.Reason);
        }

        [Fact]
        public async Task Set_ForwardsTheBranchProductAndAvailability()
        {
            var response = new BranchProductAvailabilityResponse(
                BranchId, ProductId, false, "manual", DateTimeOffset.UnixEpoch);
            var sender = new RecordingSender { Response = Result.Success(response) };
            var controller = CreateController(sender);

            var result = await controller.Set(
                "fixture-cafe", BranchId, ProductId, new SetBranchProductAvailabilityRequest(false), default);

            Assert.Same(response, Assert.IsType<OkObjectResult>(result.Result).Value);
            var command = Assert.IsType<SetBranchProductAvailabilityCommand>(Assert.Single(sender.Requests));
            Assert.Equal(BranchId, command.BranchId);
            Assert.Equal(ProductId, command.ProductId);
            Assert.False(command.Available);
            Assert.Equal("manual", command.Reason);
        }

        [Fact]
        public async Task List_UsesTheBranchFromTheRouteAndKeepsTheQueryFilters()
        {
            var page = OffsetPage<BranchProductAvailabilityItem>.Create([], 2, 20, null);
            var sender = new RecordingSender { Response = Result.Success(page) };
            var controller = CreateController(sender);
            var categoryId = Guid.NewGuid();

            var result = await controller.List(
                "fixture-cafe",
                BranchId,
                new ListBranchProductAvailabilityQuery
                {
                    Available = true,
                    CategoryId = categoryId,
                    Page = 2,
                    IncludeTotalCount = true,
                },
                default);

            Assert.Same(page, Assert.IsType<OkObjectResult>(result.Result).Value);
            var query = Assert.IsType<ListBranchProductAvailabilityQuery>(Assert.Single(sender.Requests));
            Assert.Equal(BranchId, query.BranchId);
            Assert.True(query.Available);
            Assert.Equal(categoryId, query.CategoryId);
            Assert.Equal(2, query.Page);
            Assert.True(query.IncludeTotalCount);
        }

        [Fact]
        public async Task Set_MapsNotFoundToAProblemResponse()
        {
            var sender = new RecordingSender { Response = Result.Failure<BranchProductAvailabilityResponse>(ProductErrors.NotFound()) };
            var controller = CreateController(sender);

            var result = await controller.Set(
                "fixture-cafe", BranchId, ProductId, new SetBranchProductAvailabilityRequest(false), default);

            Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ObjectResult>(result.Result).StatusCode);
        }

        private static TenantBranchProductAvailabilityController CreateController(RecordingSender sender) =>
            new(sender, new CorrelationStub())
            {
                ControllerContext = new() { HttpContext = new DefaultHttpContext() },
            };

        private sealed class CorrelationStub : ICorrelationIdAccessor
        {
            public string? CorrelationId => null;
        }

        private sealed class RecordingSender : ISender
        {
            public List<object> Requests { get; } = new();
            public object? Response { get; set; }

            public Task<TResponse> Send<TResponse>(
                IRequest<TResponse> request, CancellationToken cancellationToken = default)
            {
                Requests.Add(request);
                return Task.FromResult((TResponse)Response!);
            }

            public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
                where TRequest : IRequest => throw new NotSupportedException();

            public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

            public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
                IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

            public IAsyncEnumerable<object?> CreateStream(
                object request, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();
        }
    }
}
