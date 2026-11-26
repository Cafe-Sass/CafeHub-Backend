using System.Reflection;
using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Inventory;
using CafeHub.Application.Inventory.Commands;
using CafeHub.Application.Inventory.DTOs;
using CafeHub.Application.Inventory.Queries;
using CafeHub.Application.Pagination;
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
    public sealed class TenantInventoryControllerTests
    {
        private static readonly Guid BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        private static readonly DateOnly ReceivedDate = new(2026, 11, 25);
        private static readonly InventoryItemResponse ItemResponse =
            new(Guid.NewGuid(), "Coffee Beans", "Kg", 5m, true, 7, "Active", "1");
        private static readonly InventoryBatchResponse BatchResponse = new(
            Guid.NewGuid(), Guid.NewGuid(), BranchId, 2.5m, 2.5m, ReceivedDate, new DateOnly(2026, 12, 1), Guid.NewGuid());

        private static TenantInventoryController CreateController(ISender sender) =>
            new(sender, new TestCorrelationIdAccessor())
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-inventory" },
                },
            };

        private static MethodInfo Action(string name) =>
            typeof(TenantInventoryController).GetMethod(name, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"The TenantInventoryController.{name} action was not found.");

        [Fact]
        public void TenantInventoryController_IsAnApiControllerOnTheDocumentedRoute()
        {
            Assert.NotNull(typeof(TenantInventoryController).GetCustomAttribute<ApiControllerAttribute>());
            var route = typeof(TenantInventoryController).GetCustomAttribute<RouteAttribute>();
            Assert.NotNull(route);
            Assert.Equal("t/{slug}/api/v1/inventory", route!.Template);
        }

        [Fact]
        public void Actions_AreDeclaredOnTheDocumentedVerbsAndTemplates()
        {
            Assert.Null(Action(nameof(TenantInventoryController.Create)).GetCustomAttribute<HttpPostAttribute>()!.Template);
            Assert.Null(Action(nameof(TenantInventoryController.List)).GetCustomAttribute<HttpGetAttribute>()!.Template);
            Assert.Equal("{id:guid}", Action(nameof(TenantInventoryController.Get))
                .GetCustomAttribute<HttpGetAttribute>()!.Template);
            Assert.Equal("{id:guid}", Action(nameof(TenantInventoryController.Update))
                .GetCustomAttribute<HttpPutAttribute>()!.Template);
            Assert.Equal("{id:guid}/deactivate", Action(nameof(TenantInventoryController.Deactivate))
                .GetCustomAttribute<HttpPostAttribute>()!.Template);
            Assert.Equal("{id:guid}/reactivate", Action(nameof(TenantInventoryController.Reactivate))
                .GetCustomAttribute<HttpPostAttribute>()!.Template);
            Assert.Equal("branches/{branchId:guid}/batches", Action(nameof(TenantInventoryController.ReceiveBatch))
                .GetCustomAttribute<HttpPostAttribute>()!.Template);
            Assert.Equal("branches/{branchId:guid}/batches", Action(nameof(TenantInventoryController.ListBatches))
                .GetCustomAttribute<HttpGetAttribute>()!.Template);
            Assert.Equal("stock", Action(nameof(TenantInventoryController.Stock))
                .GetCustomAttribute<HttpGetAttribute>()!.Template);
            Assert.Equal("movements", Action(nameof(TenantInventoryController.ListMovements))
                .GetCustomAttribute<HttpGetAttribute>()!.Template);
            Assert.Equal("alerts", Action(nameof(TenantInventoryController.Alerts))
                .GetCustomAttribute<HttpGetAttribute>()!.Template);
        }

        [Fact]
        public void ReadActions_RequireInventoryViewAndWriteActionsRequireTheirPermissions()
        {
            var policies = new Dictionary<string, string>
            {
                [nameof(TenantInventoryController.List)] = TenantPermissions.InventoryView,
                [nameof(TenantInventoryController.Get)] = TenantPermissions.InventoryView,
                [nameof(TenantInventoryController.ListBatches)] = TenantPermissions.InventoryView,
                [nameof(TenantInventoryController.Stock)] = TenantPermissions.InventoryView,
                [nameof(TenantInventoryController.ListMovements)] = TenantPermissions.InventoryView,
                [nameof(TenantInventoryController.Alerts)] = TenantPermissions.InventoryView,
                [nameof(TenantInventoryController.Create)] = TenantPermissions.InventoryManage,
                [nameof(TenantInventoryController.Update)] = TenantPermissions.InventoryManage,
                [nameof(TenantInventoryController.Deactivate)] = TenantPermissions.InventoryManage,
                [nameof(TenantInventoryController.Reactivate)] = TenantPermissions.InventoryManage,
                [nameof(TenantInventoryController.ReceiveBatch)] = TenantPermissions.InventoryReceive,
            };

            foreach (var (name, policy) in policies)
            {
                var action = Action(name);
                var authorize = Assert.IsType<TenantAuthorizeAttribute>(
                    action.GetCustomAttribute<TenantAuthorizeAttribute>());
                Assert.Equal(policy, authorize.Policy);
                Assert.Equal(TenantAuthenticationSchemes.Bearer, authorize.AuthenticationSchemes);
                Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
            }

            Assert.Null(Action(nameof(TenantInventoryController.ReceiveBatch))
                .GetCustomAttribute<AllowAnonymousAttribute>());
            Assert.Equal("inventory.view", TenantPermissions.InventoryView);
            Assert.True(TenantPermissions.IsStaffGrant(TenantPermissions.InventoryView));
        }

        [Fact]
        public async Task Create_ParsesTheUnitAndReturnsCreatedAtTheItemDetail()
        {
            var sender = new RecordingSender { Response = Result.Success(ItemResponse) };
            var controller = CreateController(sender);

            var actionResult = await controller.Create(
                "fixture-cafe",
                new CreateInventoryItemRequest("Coffee Beans", "kg", 5m, true, null),
                CancellationToken.None);

            var created = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
            Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
            Assert.Equal(nameof(TenantInventoryController.Get), created.ActionName);
            Assert.Equal("fixture-cafe", created.RouteValues!["slug"]);
            Assert.Equal(ItemResponse.Id, created.RouteValues["id"]);
            var command = Assert.IsType<CreateInventoryItemCommand>(sender.LastRequest);
            Assert.Equal("Coffee Beans", command.Name);
            Assert.Equal(InventoryUnit.Kg, command.Unit);
            Assert.Equal(5m, command.LowStockThreshold);
            Assert.True(command.TracksExpiry);
            Assert.Null(command.NearExpiryDays);
        }

        [Fact]
        public async Task Create_WhenTheUnitIsUnknown_ReturnsTheInvalidUnitValidationError()
        {
            var sender = new RecordingSender();
            var controller = CreateController(sender);

            var actionResult = await controller.Create(
                "fixture-cafe",
                new CreateInventoryItemRequest("Coffee Beans", "barrel", 5m, true, 7),
                CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);
            Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
            var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
            Assert.Equal("Inventory.InvalidUnit", problem.Extensions["code"]);
            Assert.Null(sender.LastRequest);
        }

        [Fact]
        public async Task List_SendsTheListItemsQueryAndReturnsThePage()
        {
            var page = new OffsetPage<InventoryItemResponse>
            {
                Items = new[] { ItemResponse },
                Page = 1,
                PageSize = 20,
                TotalCount = 1,
                HasNextPage = false,
            };
            var sender = new RecordingSender { Response = Result.Success(page) };
            var controller = CreateController(sender);
            var query = new ListInventoryItemsQuery { Page = 1, PageSize = 20, IncludeTotalCount = true };

            var actionResult = await controller.List("fixture-cafe", query, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            Assert.Same(page, okResult.Value);
            Assert.Same(query, sender.LastRequest);
        }

        [Fact]
        public async Task Get_WhenTheItemIsMissing_MapsTo404()
        {
            var sender = new RecordingSender
            {
                Response = Result.Failure<InventoryItemResponse>(InventoryItemErrors.NotFound()),
            };
            var controller = CreateController(sender);
            var id = Guid.NewGuid();

            var actionResult = await controller.Get("fixture-cafe", id, CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);
            Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
            var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
            Assert.Equal("Inventory.ItemNotFound", problem.Extensions["code"]);
            Assert.Equal(id, Assert.IsType<GetInventoryItemQuery>(sender.LastRequest).Id);
        }

        [Fact]
        public async Task Update_ParsesTheUnitAndSendsTheConcurrencyToken()
        {
            var sender = new RecordingSender { Response = Result.Success(ItemResponse) };
            var controller = CreateController(sender);
            var id = Guid.NewGuid();

            var actionResult = await controller.Update(
                "fixture-cafe",
                id,
                new UpdateInventoryItemRequest("Tea", "Pack", 2m, false, 0, "13"),
                CancellationToken.None);

            Assert.IsType<OkObjectResult>(actionResult.Result);
            var command = Assert.IsType<UpdateInventoryItemCommand>(sender.LastRequest);
            Assert.Equal(id, command.Id);
            Assert.Equal("Tea", command.Name);
            Assert.Equal(InventoryUnit.Pack, command.Unit);
            Assert.False(command.TracksExpiry);
            Assert.Equal(0, command.NearExpiryDays);
            Assert.Equal("13", command.ConcurrencyToken);
        }

        [Fact]
        public async Task LifecycleActions_SendTheirCommands()
        {
            var sender = new RecordingSender { Response = Result.Success(ItemResponse) };
            var controller = CreateController(sender);
            var id = Guid.NewGuid();

            await controller.Deactivate(
                "fixture-cafe", id, new InventoryItemLifecycleRequest("7"), CancellationToken.None);
            var deactivate = Assert.IsType<DeactivateInventoryItemCommand>(sender.LastRequest);
            Assert.Equal(id, deactivate.Id);
            Assert.Equal("7", deactivate.ConcurrencyToken);

            await controller.Reactivate(
                "fixture-cafe", id, new InventoryItemLifecycleRequest("8"), CancellationToken.None);
            var reactivate = Assert.IsType<ReactivateInventoryItemCommand>(sender.LastRequest);
            Assert.Equal(id, reactivate.Id);
            Assert.Equal("8", reactivate.ConcurrencyToken);
        }

        [Fact]
        public async Task ReceiveBatch_BindsTheRouteHeaderAndBodyAndReturnsCreated()
        {
            var sender = new RecordingSender { Response = Result.Success(BatchResponse) };
            var controller = CreateController(sender);

            var actionResult = await controller.ReceiveBatch(
                "fixture-cafe",
                BranchId,
                new ReceiveStockRequest(BatchResponse.ItemId, 2.5m, ReceivedDate, new DateOnly(2026, 12, 1)),
                "receipt-key-1",
                CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);
            Assert.Equal(StatusCodes.Status201Created, objectResult.StatusCode);
            Assert.Same(BatchResponse, objectResult.Value);
            var command = Assert.IsType<ReceiveStockCommand>(sender.LastRequest);
            Assert.Equal(BranchId, command.BranchId);
            Assert.Equal(BatchResponse.ItemId, command.ItemId);
            Assert.Equal(2.5m, command.ReceivedQuantity);
            Assert.Equal(ReceivedDate, command.ReceivedDate);
            Assert.Equal(new DateOnly(2026, 12, 1), command.ExpiryDate);
            Assert.Equal("receipt-key-1", command.IdempotencyKey);
        }

        [Fact]
        public async Task ListBatches_PinsTheBranchFromTheRouteAndForwardsTheFilters()
        {
            var page = new OffsetPage<InventoryBatchResponse>
            {
                Items = new[] { BatchResponse },
                Page = 2,
                PageSize = 10,
                TotalCount = null,
                HasNextPage = false,
            };
            var sender = new RecordingSender { Response = Result.Success(page) };
            var controller = CreateController(sender);
            var itemId = Guid.NewGuid();

            var actionResult = await controller.ListBatches(
                "fixture-cafe",
                BranchId,
                new ListInventoryBatchesQuery
                {
                    BranchId = Guid.NewGuid(),
                    ItemId = itemId,
                    ExpiryFrom = new DateOnly(2026, 12, 1),
                    Page = 2,
                    PageSize = 10,
                },
                CancellationToken.None);

            Assert.IsType<OkObjectResult>(actionResult.Result);
            var query = Assert.IsType<ListInventoryBatchesQuery>(sender.LastRequest);
            Assert.Equal(BranchId, query.BranchId);
            Assert.Equal(itemId, query.ItemId);
            Assert.Equal(new DateOnly(2026, 12, 1), query.ExpiryFrom);
            Assert.Equal(2, query.Page);
            Assert.Equal(10, query.PageSize);
        }

        [Fact]
        public async Task Stock_SendsTheStockQueryAndReturnsThePage()
        {
            var page = new OffsetPage<StockLevelResponse>
            {
                Items = new[] { new StockLevelResponse(BatchResponse.ItemId, BranchId, 2.5m, ReceivedDate) },
                Page = 1,
                PageSize = 20,
                TotalCount = 1,
                HasNextPage = false,
            };
            var sender = new RecordingSender { Response = Result.Success(page) };
            var controller = CreateController(sender);
            var query = new ListStockLevelsQuery { BranchId = BranchId, Page = 1, PageSize = 20 };

            var actionResult = await controller.Stock("fixture-cafe", query, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            Assert.Same(page, okResult.Value);
            Assert.Same(query, sender.LastRequest);
        }

        [Fact]
        public async Task Alerts_SendsTheAlertQueryAndReturnsThePage()
        {
            var alert = new InventoryAlertResponse(
                Guid.NewGuid(), BatchResponse.ItemId, "Coffee Beans", BranchId, null,
                "LowStock", "Open", 1.5m, null,
                new DateTimeOffset(2026, 11, 25, 9, 0, 0, TimeSpan.Zero), null, null);
            var page = new OffsetPage<InventoryAlertResponse>
            {
                Items = new[] { alert },
                Page = 1,
                PageSize = 25,
                TotalCount = 1,
                HasNextPage = false,
            };
            var sender = new RecordingSender { Response = Result.Success(page) };
            var controller = CreateController(sender);
            var query = new ListInventoryAlertsQuery
            {
                Kind = InventoryAlertKind.NearExpiry,
                State = InventoryAlertState.Open,
                BranchId = BranchId,
            };

            var actionResult = await controller.Alerts("fixture-cafe", query, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            Assert.Same(page, okResult.Value);
            Assert.Same(query, sender.LastRequest);
        }

        [Fact]
        public async Task ListMovements_SendsTheMovementQueryAndReturnsThePage()
        {
            var movement = new StockMovementResponse(
                Guid.NewGuid(), BatchResponse.ItemId, BranchId, BatchResponse.Id,
                "Consume", -1m, "usage", new DateTimeOffset(2026, 11, 25, 9, 0, 0, TimeSpan.Zero),
                Guid.NewGuid());
            var page = new OffsetPage<StockMovementResponse>
            {
                Items = new[] { movement },
                Page = 2,
                PageSize = 10,
                TotalCount = null,
                HasNextPage = false,
            };
            var sender = new RecordingSender { Response = Result.Success(page) };
            var controller = CreateController(sender);
            var query = new ListStockMovementsQuery
            {
                BranchId = BranchId,
                ItemId = movement.ItemId,
                Type = StockMovementType.Waste,
                CreatedFrom = ReceivedDate,
            };

            var actionResult = await controller.ListMovements("fixture-cafe", query, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            Assert.Same(page, okResult.Value);
            Assert.Same(query, sender.LastRequest);
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
            public string? CorrelationId => "correlation-inventory";
        }
    }
}
