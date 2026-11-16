using System.Reflection;
using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Pagination;
using CafeHub.Application.Stations;
using CafeHub.Application.Stations.Commands;
using CafeHub.Application.Stations.DTOs;
using CafeHub.Application.Stations.Queries;
using CafeHub.WebApi.Authentication;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Controllers;
using CafeHub.WebApi.Errors;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.UnitTests.Stations
{
    public sealed class TenantStationsControllerTests
    {
        private const string TraceIdentifier = "trace-stations";

        private static TenantStationsController CreateController(
            ISender sender,
            ICorrelationIdAccessor? correlationIdAccessor = null)
        {
            return new TenantStationsController(sender, correlationIdAccessor)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { TraceIdentifier = TraceIdentifier },
                },
            };
        }

        private static StationResponse CreateStationResponse() =>
            new()
            {
                Id = Guid.NewGuid(),
                BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                Name = "Bar",
                Status = "Active",
                SortOrder = 0,
                CreatedAt = new DateTimeOffset(2026, 11, 15, 0, 0, 0, TimeSpan.Zero),
                UpdatedAt = new DateTimeOffset(2026, 11, 15, 0, 0, 0, TimeSpan.Zero),
                ConcurrencyToken = "12345",
            };

        private static MethodInfo Action(string name) =>
            typeof(TenantStationsController).GetMethod(name, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"The TenantStationsController.{name} action was not found.");

        [Fact]
        public void TenantStationsController_IsAnApiControllerOnTheDocumentedRoute()
        {
            Assert.True(typeof(ControllerBase).IsAssignableFrom(typeof(TenantStationsController)));
            Assert.NotNull(typeof(TenantStationsController).GetCustomAttribute<ApiControllerAttribute>());
            var route = typeof(TenantStationsController).GetCustomAttribute<RouteAttribute>();
            Assert.NotNull(route);
            Assert.Equal("t/{slug}/api/v1/branches/{branchId:guid}/stations", route!.Template);
        }

        [Fact]
        public void Actions_AreDeclaredOnTheDocumentedVerbsAndTemplates()
        {
            var create = Action(nameof(TenantStationsController.Create));
            var createPost = create.GetCustomAttribute<HttpPostAttribute>();
            Assert.NotNull(createPost);
            Assert.Null(createPost!.Template);

            var list = Action(nameof(TenantStationsController.List));
            var listGet = list.GetCustomAttribute<HttpGetAttribute>();
            Assert.NotNull(listGet);
            Assert.Null(listGet!.Template);

            var get = Action(nameof(TenantStationsController.Get));
            var getGet = get.GetCustomAttribute<HttpGetAttribute>();
            Assert.NotNull(getGet);
            Assert.Equal("{stationId:guid}", getGet!.Template);

            var put = Action(nameof(TenantStationsController.Put));
            var putHttpPut = put.GetCustomAttribute<HttpPutAttribute>();
            Assert.NotNull(putHttpPut);
            Assert.Equal("{stationId:guid}", putHttpPut!.Template);

            var deactivate = Action(nameof(TenantStationsController.Deactivate));
            var deactivatePost = deactivate.GetCustomAttribute<HttpPostAttribute>();
            Assert.NotNull(deactivatePost);
            Assert.Equal("{stationId:guid}/deactivate", deactivatePost!.Template);

            var reactivate = Action(nameof(TenantStationsController.Reactivate));
            var reactivatePost = reactivate.GetCustomAttribute<HttpPostAttribute>();
            Assert.NotNull(reactivatePost);
            Assert.Equal("{stationId:guid}/reactivate", reactivatePost!.Template);
        }

        [Fact]
        public void ReadActions_RequireTenantUserPolicy()
        {
            foreach (var name in new[] { nameof(TenantStationsController.List), nameof(TenantStationsController.Get) })
            {
                var action = Action(name);
                var authorize = action.GetCustomAttribute<TenantAuthorizeAttribute>();
                Assert.NotNull(authorize);
                Assert.Equal(TenantPolicies.TenantUser, authorize!.Policy);
                Assert.Equal(TenantAuthenticationSchemes.Bearer, authorize.AuthenticationSchemes);
                Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
            }

            Assert.Null(typeof(TenantStationsController).GetCustomAttribute<AllowAnonymousAttribute>());
        }

        [Fact]
        public void WriteActions_RequireStationsManagePermission()
        {
            foreach (var name in new[]
            {
                nameof(TenantStationsController.Create),
                nameof(TenantStationsController.Put),
                nameof(TenantStationsController.Deactivate),
                nameof(TenantStationsController.Reactivate),
            })
            {
                var action = Action(name);
                var authorize = action.GetCustomAttribute<TenantAuthorizeAttribute>();
                Assert.NotNull(authorize);
                Assert.Equal(TenantPermissions.StationsManage, authorize!.Policy);
                Assert.Equal(TenantAuthenticationSchemes.Bearer, authorize.AuthenticationSchemes);
                Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
            }
        }

        [Fact]
        public async Task Create_SendsAddStationCommandAndReturnsCreatedAtStationDetail()
        {
            var response = CreateStationResponse();
            var sender = new RecordingSender { Response = Result.Success(response) };
            var controller = CreateController(sender);
            var request = new CreateStationRequest("Bar", 1);

            var actionResult = await controller.Create("fixture-cafe", Guid.NewGuid(), request, CancellationToken.None);

            var created = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
            Assert.Equal(nameof(TenantStationsController.Get), created.ActionName);
            var routeValues = created.RouteValues;
            Assert.NotNull(routeValues);
            Assert.Equal("fixture-cafe", routeValues["slug"]);
            Assert.Equal(response.Id, routeValues["stationId"]);
            Assert.Same(response, created.Value);
            var command = Assert.IsType<CreateStationCommand>(sender.LastRequest);
            Assert.Equal("Bar", command.Name);
            Assert.Equal(1, command.SortOrder);
        }

        [Fact]
        public async Task Create_WhenNotFound_MapsTo404()
        {
            var sender = new RecordingSender { Response = Result.Failure<StationResponse>(StationsErrors.NotFound()) };
            var controller = CreateController(sender);

            var actionResult = await controller.Create(
                "fixture-cafe",
                Guid.NewGuid(),
                new CreateStationRequest("Bar", 1),
                CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);
            Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
            var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
            Assert.Equal(StationsErrors.NotFoundCode, problem.Extensions["code"]);
        }

        [Fact]
        public async Task List_SendsListStationsQueryAndReturnsPage()
        {
            var response = CreateStationResponse();
            var page = new OffsetPage<StationResponse>
            {
                Items = new[] { response },
                Page = 1,
                PageSize = 20,
                TotalCount = 1,
                HasNextPage = false,
            };
            var sender = new RecordingSender { Response = Result.Success(page) };
            var controller = CreateController(sender);
            var branchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

            var actionResult = await controller.List(
                "fixture-cafe",
                branchId,
                new ListStationsQuery { BranchId = branchId, Page = 1, PageSize = 20, IncludeTotalCount = true },
                CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var returnedPage = Assert.IsType<OffsetPage<StationResponse>>(okResult.Value);
            Assert.Single(returnedPage.Items);
            Assert.Equal(1, returnedPage.TotalCount);
            var sentQuery = Assert.IsType<ListStationsQuery>(sender.LastRequest);
            Assert.Equal(branchId, sentQuery.BranchId);
            Assert.Equal(1, sentQuery.Page);
            Assert.Equal(20, sentQuery.PageSize);
            Assert.True(sentQuery.IncludeTotalCount);
        }

        [Fact]
        public async Task Get_SendsGetStationQueryAndReturnsStation()
        {
            var response = CreateStationResponse();
            var sender = new RecordingSender { Response = Result.Success(response) };
            var controller = CreateController(sender);
            var stationId = Guid.NewGuid();

            var actionResult = await controller.Get("fixture-cafe", Guid.NewGuid(), stationId, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var returnedStation = Assert.IsType<StationResponse>(okResult.Value);
            Assert.Equal(response.Id, returnedStation.Id);
            Assert.Equal(response.Name, returnedStation.Name);
            Assert.Equal(response.Status, returnedStation.Status);
            var query = Assert.IsType<GetStationQuery>(sender.LastRequest);
            Assert.Equal(stationId, query.StationId);
        }

        [Fact]
        public async Task Get_WhenNotFound_MapsTo404()
        {
            var sender = new RecordingSender { Response = Result.Failure<StationResponse>(StationsErrors.NotFound()) };
            var controller = CreateController(sender);

            var actionResult = await controller.Get("fixture-cafe", Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);
            Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
            var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
            Assert.Equal(StationsErrors.NotFoundCode, problem.Extensions["code"]);
        }

        [Fact]
        public async Task Put_SendsUpdateStationCommandAndReturnsStation()
        {
            var response = CreateStationResponse();
            var sender = new RecordingSender { Response = Result.Success(response) };
            var controller = CreateController(sender);
            var stationId = Guid.NewGuid();

            var actionResult = await controller.Put(
                "fixture-cafe",
                Guid.NewGuid(),
                stationId,
                new UpdateStationRequest("Barista", 2, "12345"),
                CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var returnedStation = Assert.IsType<StationResponse>(okResult.Value);
            Assert.Equal(response.Id, returnedStation.Id);
            var command = Assert.IsType<UpdateStationCommand>(sender.LastRequest);
            Assert.Equal(stationId, command.StationId);
            Assert.Equal("Barista", command.Name);
            Assert.Equal(2, command.SortOrder);
            Assert.Equal("12345", command.ConcurrencyToken);
        }

        [Fact]
        public async Task Deactivate_SendsDeactivateStationCommandAndReturnsStation()
        {
            var response = CreateStationResponse();
            var sender = new RecordingSender { Response = Result.Success(response) };
            var controller = CreateController(sender);
            var stationId = Guid.NewGuid();

            var actionResult = await controller.Deactivate(
                "fixture-cafe",
                Guid.NewGuid(),
                stationId,
                new StationLifecycleRequest("12345"),
                CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var returnedStation = Assert.IsType<StationResponse>(okResult.Value);
            Assert.Equal(response.Id, returnedStation.Id);
            var command = Assert.IsType<DeactivateStationCommand>(sender.LastRequest);
            Assert.Equal(stationId, command.StationId);
            Assert.Equal("12345", command.ConcurrencyToken);
        }

        [Fact]
        public async Task Reactivate_SendsReactivateStationCommandAndReturnsStation()
        {
            var response = CreateStationResponse();
            var sender = new RecordingSender { Response = Result.Success(response) };
            var controller = CreateController(sender);
            var stationId = Guid.NewGuid();

            var actionResult = await controller.Reactivate(
                "fixture-cafe",
                Guid.NewGuid(),
                stationId,
                new StationLifecycleRequest("12345"),
                CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var returnedStation = Assert.IsType<StationResponse>(okResult.Value);
            Assert.Equal(response.Id, returnedStation.Id);
            var command = Assert.IsType<ReactivateStationCommand>(sender.LastRequest);
            Assert.Equal(stationId, command.StationId);
            Assert.Equal("12345", command.ConcurrencyToken);
        }

        [Fact]
        public void TheController_RequiresTheSender()
        {
            Assert.Throws<ArgumentNullException>(() => new TenantStationsController(null!));
        }

        private sealed class RecordingSender : ISender
        {
            public object? Response { get; set; }
            public object? LastRequest { get; private set; }

            public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
            {
                LastRequest = request;
                return Task.FromResult((TResponse)Response!);
            }

            public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest =>
                throw new NotSupportedException("The controller must send a request with a response.");

            public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException("The controller must send a typed request.");

            public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException("The controller does not create streams.");

            public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException("The controller does not create streams.");
        }

        private sealed class TestCorrelationIdAccessor(string? correlationId) : ICorrelationIdAccessor
        {
            public TestCorrelationIdAccessor() : this(null) { }

            public string? CorrelationId { get; } = correlationId;
        }
    }
}
