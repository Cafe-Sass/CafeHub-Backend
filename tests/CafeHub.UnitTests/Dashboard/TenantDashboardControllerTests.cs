using System.Reflection;
using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Dashboard;
using CafeHub.Application.Dashboard.DTOs;
using CafeHub.Application.Dashboard.Queries;
using CafeHub.Application.Identity;
using CafeHub.WebApi.Authentication;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Controllers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.UnitTests.Dashboard
{
    public sealed class TenantDashboardControllerTests
    {
        private static readonly Guid BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        private static readonly DateOnly From = new(2026, 11, 1);
        private static readonly DateOnly To = new(2026, 11, 2);

        private static DashboardResponse EmptyDashboard() => new(
            DashboardMetricCatalog.All,
            new DashboardOverviewMetrics(
                0m,
                0m,
                0,
                null,
                new DashboardPaymentSplit(0m, 0m),
                [],
                [],
                [],
                new DashboardWifiAttachRate(null, DashboardInsightsRules.NoDineInOrdersReason),
                []));

        private static TenantDashboardController CreateController(ISender sender) =>
            new(sender, new TestCorrelationIdAccessor())
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-dashboard" },
                },
            };

        private static MethodInfo Action(string name) =>
            typeof(TenantDashboardController).GetMethod(name, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"The TenantDashboardController.{name} action was not found.");

        [Fact]
        public void Controller_IsAnApiControllerOnTheDashboardRoute()
        {
            Assert.NotNull(typeof(TenantDashboardController).GetCustomAttribute<ApiControllerAttribute>());
            var route = typeof(TenantDashboardController).GetCustomAttribute<RouteAttribute>();
            Assert.NotNull(route);
            Assert.Equal("t/{slug}/api/v1/dashboard", route!.Template);
        }

        [Fact]
        public void Get_IsAGetRequiringReportsView()
        {
            Assert.NotNull(Action(nameof(TenantDashboardController.Get)).GetCustomAttribute<HttpGetAttribute>());
            var authorize = Assert.IsType<TenantAuthorizeAttribute>(
                Action(nameof(TenantDashboardController.Get)).GetCustomAttribute<TenantAuthorizeAttribute>());
            Assert.Equal(TenantPermissions.ReportsView, authorize.Policy);
            Assert.Equal(TenantAuthenticationSchemes.Bearer, authorize.AuthenticationSchemes);
            Assert.Null(Action(nameof(TenantDashboardController.Get)).GetCustomAttribute<AllowAnonymousAttribute>());
            Assert.Equal("reports.view", TenantPermissions.ReportsView);
        }

        [Fact]
        public void GetMetric_IsAGetOnTheMetricRouteRequiringReportsView()
        {
            var httpGet = Assert.IsType<HttpGetAttribute>(
                Action(nameof(TenantDashboardController.GetMetric)).GetCustomAttribute<HttpGetAttribute>());
            Assert.Equal("{metric}", httpGet.Template);
            var authorize = Assert.IsType<TenantAuthorizeAttribute>(
                Action(nameof(TenantDashboardController.GetMetric)).GetCustomAttribute<TenantAuthorizeAttribute>());
            Assert.Equal(TenantPermissions.ReportsView, authorize.Policy);
            Assert.Null(Action(nameof(TenantDashboardController.GetMetric)).GetCustomAttribute<AllowAnonymousAttribute>());
        }

        [Fact]
        public void ReportsView_JoinsThePermissionCatalogAsAStaffGrant()
        {
            Assert.Contains(TenantPermissions.ReportsView, TenantPermissions.All);
            Assert.True(TenantPermissions.IsStaffGrant(TenantPermissions.ReportsView));
            Assert.False(TenantPermissions.IsOwnerOnly(TenantPermissions.ReportsView));
        }

        [Fact]
        public async Task Get_SendsTheRangeAndBranchAndReturnsTheDashboard()
        {
            var response = EmptyDashboard();
            var sender = new RecordingSender { Response = Result.Success(response) };
            var controller = CreateController(sender);

            var actionResult = await controller.Get(From, To, BranchId, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
            Assert.Same(response, okResult.Value);
            var query = Assert.IsType<GetDashboardQuery>(sender.LastRequest);
            Assert.Equal(From, query.From);
            Assert.Equal(To, query.To);
            Assert.Equal(BranchId, query.BranchId);
        }

        [Fact]
        public async Task GetMetric_SendsTheMetricKeyAndRange()
        {
            var response = new DashboardMetricResponse(DashboardMetricCatalog.All[0], 10m);
            var sender = new RecordingSender { Response = Result.Success(response) };
            var controller = CreateController(sender);

            var actionResult = await controller.GetMetric("sales", From, To, null, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            Assert.Same(response, okResult.Value);
            var query = Assert.IsType<GetDashboardMetricQuery>(sender.LastRequest);
            Assert.Equal("sales", query.Metric);
            Assert.Equal(From, query.From);
            Assert.Equal(To, query.To);
            Assert.Null(query.BranchId);
        }

        [Fact]
        public async Task GetMetric_MapsAnUnknownMetricToNotFound()
        {
            var sender = new RecordingSender
            {
                Response = Result.Failure<DashboardMetricResponse>(DashboardErrors.UnknownMetric("profit")),
            };
            var controller = CreateController(sender);

            var actionResult = await controller.GetMetric("profit", From, To, null, CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);
            Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
            var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
            Assert.Equal(DashboardErrors.UnknownMetricCode, problem.Extensions["code"]);
        }

        [Fact]
        public async Task Get_MapsAValidationFailureToABadRequest()
        {
            var sender = new RecordingSender
            {
                Response = Result.Failure<DashboardResponse>(
                    DashboardErrors.InvalidRange("The range end must be after the range start.")),
            };
            var controller = CreateController(sender);

            var actionResult = await controller.Get(To, From, null, CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);
            Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
            var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
            Assert.Equal(DashboardErrors.InvalidRangeCode, problem.Extensions["code"]);
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
            public string? CorrelationId => "correlation-dashboard";
        }
    }
}
