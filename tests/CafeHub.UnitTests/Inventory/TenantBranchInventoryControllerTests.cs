using System.Reflection;
using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Inventory;
using CafeHub.Application.Inventory.Commands;
using CafeHub.Application.Inventory.DTOs;
using CafeHub.Domain.Tenants;
using CafeHub.WebApi.Authentication;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Controllers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.UnitTests.Inventory
{
    public sealed class TenantBranchInventoryControllerTests
    {
        private static readonly Guid BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        private static readonly Guid ItemId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        private static readonly Guid BatchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly DateTimeOffset Now = new(2026, 11, 25, 9, 0, 0, TimeSpan.Zero);
        private static readonly StockMovementResponse Response = new(
            Guid.NewGuid(), ItemId, BranchId, BatchId, "Waste", -1m, "spoiled", Now, Guid.NewGuid());

        private static TenantBranchInventoryController CreateController(ISender sender) =>
            new(sender, new TestCorrelationIdAccessor())
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-branch-inventory" },
                },
            };

        private static MethodInfo Action() =>
            typeof(TenantBranchInventoryController)
                .GetMethod(nameof(TenantBranchInventoryController.RecordMovement), BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException(
                "The TenantBranchInventoryController.RecordMovement action was not found.");

        [Fact]
        public void Controller_IsAnApiControllerOnTheBranchInventoryRoute()
        {
            Assert.NotNull(typeof(TenantBranchInventoryController).GetCustomAttribute<ApiControllerAttribute>());
            var route = typeof(TenantBranchInventoryController).GetCustomAttribute<RouteAttribute>();
            Assert.NotNull(route);
            Assert.Equal("t/{slug}/api/v1/branches/{branchId:guid}/inventory", route!.Template);
        }

        [Fact]
        public void RecordMovement_IsAPostRequiringInventoryMove()
        {
            var httpPost = Assert.IsType<HttpPostAttribute>(Action().GetCustomAttribute<HttpPostAttribute>());
            Assert.Equal("movements", httpPost.Template);
            var authorize = Assert.IsType<TenantAuthorizeAttribute>(
                Action().GetCustomAttribute<TenantAuthorizeAttribute>());
            Assert.Equal(TenantPermissions.InventoryMove, authorize.Policy);
            Assert.Equal(TenantAuthenticationSchemes.Bearer, authorize.AuthenticationSchemes);
            Assert.Null(Action().GetCustomAttribute<AllowAnonymousAttribute>());
            Assert.Equal("inventory.move", TenantPermissions.InventoryMove);
        }

        [Fact]
        public async Task RecordMovement_ParsesTheTypeAndForwardsTheHeader()
        {
            var sender = new RecordingSender { Response = Result.Success<IReadOnlyList<StockMovementResponse>>(new[] { Response }) };
            var controller = CreateController(sender);

            var actionResult = await controller.RecordMovement(
                "fixture-cafe",
                BranchId,
                new RecordStockMovementRequest(ItemId, null, "waste", 1m, "spoiled"),
                "movement-key-9",
                CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
            Assert.Same(Response, Assert.Single((IReadOnlyList<StockMovementResponse>)okResult.Value!));
            var command = Assert.IsType<RecordStockMovementCommand>(sender.LastRequest);
            Assert.Equal(BranchId, command.BranchId);
            Assert.Equal(ItemId, command.ItemId);
            Assert.Null(command.BatchId);
            Assert.Equal(StockMovementType.Waste, command.Type);
            Assert.Equal(1m, command.Quantity);
            Assert.Equal("spoiled", command.Reason);
            Assert.Equal("movement-key-9", command.IdempotencyKey);
        }

        [Fact]
        public async Task RecordMovement_RejectsReceiveAndUnknownTypesBeforeDispatching()
        {
            var sender = new RecordingSender();
            var controller = CreateController(sender);

            foreach (var type in new[] { "Receive", "Brew" })
            {
                var actionResult = await controller.RecordMovement(
                    "fixture-cafe",
                    BranchId,
                    new RecordStockMovementRequest(ItemId, BatchId, type, 1m, "reason"),
                    "movement-key-9",
                    CancellationToken.None);

                var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);
                Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
                var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
                Assert.Equal("Inventory.InvalidMovementType", problem.Extensions["code"]);
            }

            Assert.Null(sender.LastRequest);
        }

        [Fact]
        public async Task RecordMovement_MapsADomainFailureToProblemDetails()
        {
            var sender = new RecordingSender
            {
                Response = Result.Failure<IReadOnlyList<StockMovementResponse>>(
                    StockMovementErrors.InsufficientStock()),
            };
            var controller = CreateController(sender);

            var actionResult = await controller.RecordMovement(
                "fixture-cafe",
                BranchId,
                new RecordStockMovementRequest(ItemId, null, "Consume", 1m, "usage"),
                "movement-key-9",
                CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);
            Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
            var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
            Assert.Equal("Inventory.InsufficientStock", problem.Extensions["code"]);
        }

        private sealed class RecordingSender : ISender
        {
            public object? Response { get; set; }

            public object? LastRequest { get; private set; }

            public Task<TResponse> Send<TResponse>(
                IRequest<TResponse> request, CancellationToken cancellationToken = default)
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
                IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException("The controller does not create streams.");

            public IAsyncEnumerable<object?> CreateStream(
                object request, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException("The controller does not create streams.");
        }

        private sealed class TestCorrelationIdAccessor : ICorrelationIdAccessor
        {
            public string? CorrelationId => "correlation-branch-inventory";
        }
    }
}
