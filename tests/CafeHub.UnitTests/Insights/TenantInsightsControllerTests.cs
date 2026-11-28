using System.Reflection;
using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Dashboard;
using CafeHub.Application.Identity;
using CafeHub.Application.Insights;
using CafeHub.Application.Insights.DTOs;
using CafeHub.Application.Insights.Queries;
using CafeHub.WebApi.Authentication;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Controllers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.UnitTests.Insights
{
    public sealed class TenantInsightsControllerTests
    {
        private static readonly Guid BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        private static readonly DateOnly From = new(2026, 11, 1);
        private static readonly DateOnly To = new(2026, 11, 2);

        private static MethodInfo Action(string name) =>
            typeof(TenantInsightsController).GetMethod(name, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"The TenantInsightsController.{name} action was not found.");

        private static TenantInsightsController CreateController(ISender sender) =>
            new(sender, new TestCorrelationIdAccessor())
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-insights" },
                },
            };

        private static InsightsResponse EmptyInsights() => new(
            BusinessInsightSources.Rules,
            DateTimeOffset.UtcNow,
            [],
            InsightsReasons.NoSignals);

        [Fact]
        public void Controller_IsAnApiControllerOnTheInsightsRoute()
        {
            Assert.NotNull(typeof(TenantInsightsController).GetCustomAttribute<ApiControllerAttribute>());
            var route = typeof(TenantInsightsController).GetCustomAttribute<RouteAttribute>();
            Assert.NotNull(route);
            Assert.Equal("t/{slug}/api/v1/insights", route!.Template);
        }

        [Fact]
        public void Get_IsAGetRequiringInsightsView()
        {
            var action = Action(nameof(TenantInsightsController.Get));

            Assert.NotNull(action.GetCustomAttribute<HttpGetAttribute>());
            var authorize = Assert.IsType<TenantAuthorizeAttribute>(
                action.GetCustomAttribute<TenantAuthorizeAttribute>());
            Assert.Equal(TenantPermissions.InsightsView, authorize.Policy);
            Assert.Equal(TenantAuthenticationSchemes.Bearer, authorize.AuthenticationSchemes);
            Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
            Assert.Equal("insights.view", TenantPermissions.InsightsView);
        }

        [Fact]
        public void InsightsView_JoinsThePermissionCatalogAsAStaffGrant()
        {
            Assert.Contains(TenantPermissions.InsightsView, TenantPermissions.All);
            Assert.True(TenantPermissions.IsStaffGrant(TenantPermissions.InsightsView));
            Assert.False(TenantPermissions.IsOwnerOnly(TenantPermissions.InsightsView));
        }

        [Fact]
        public async Task Get_SendsTheRangeAndBranchAndReturnsTheCards()
        {
            var response = EmptyInsights();
            var sender = new RecordingSender { Response = Result.Success(response) };
            var controller = CreateController(sender);

            var actionResult = await controller.Get(From, To, BranchId, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
            Assert.Same(response, okResult.Value);
            var query = Assert.IsType<GetInsightsQuery>(sender.LastRequest);
            Assert.Equal(From, query.From);
            Assert.Equal(To, query.To);
            Assert.Equal(BranchId, query.BranchId);
        }

        [Fact]
        public async Task Get_MapsAValidationFailureToABadRequest()
        {
            var sender = new RecordingSender
            {
                Response = Result.Failure<InsightsResponse>(
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

        private sealed class TestCorrelationIdAccessor : ICorrelationIdAccessor
        {
            public string? CorrelationId => "correlation-insights";
        }
    }
}
