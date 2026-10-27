using System.Reflection;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Identity.DTOs;
using CafeHub.Application.Identity.Queries;
using CafeHub.WebApi.Authentication;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Controllers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.UnitTests
{
    public sealed class TenantMeControllerTests
    {
        private const string TraceIdentifier = "trace-tenant-me";

        private static TenantMeController CreateController(ISender sender)
        {
            return new TenantMeController(sender, new FixedCorrelationIdAccessor("corr-tenant-me"))
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { TraceIdentifier = TraceIdentifier },
                },
            };
        }

        [Fact]
        public async Task Me_SendsTheCurrentTenantUserQuery_AndReturnsTheProfileOnSuccess()
        {
            var response = new TenantMeResponse
            {
                UserId = Guid.NewGuid(),
                Email = "owner@example.com",
                FullName = "Cafe Owner",
                UserKind = "Owner",
                Roles = new[] { "Owner" },
                TenantSlug = "cafe-one",
            };
            var sender = new RecordingSender { Response = Result.Success(response) };
            var controller = CreateController(sender);

            var actionResult = await controller.Me(CancellationToken.None);

            Assert.Same(response, Assert.IsType<OkObjectResult>(actionResult.Result).Value);
            Assert.IsType<GetCurrentTenantUserQuery>(sender.LastRequest);
        }

        [Fact]
        public async Task Me_WhenTheQueryRejectsTheCurrentUser_MapsTheGenericUnauthorizedProblem()
        {
            var sender = new RecordingSender
            {
                Response = Result.Failure<TenantMeResponse>(AuthErrors.CurrentTenantUserUnavailable()),
            };
            var controller = CreateController(sender);

            var actionResult = await controller.Me(CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);
            Assert.Equal(StatusCodes.Status401Unauthorized, objectResult.StatusCode);
            var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
            Assert.Equal(StatusCodes.Status401Unauthorized, problem.Status);
            Assert.Equal(AuthErrors.CurrentTenantUserUnavailableCode, problem.Extensions["code"]);
            Assert.Equal(AuthErrors.CurrentTenantUserUnavailableMessage, problem.Detail);
            Assert.Equal(TraceIdentifier, problem.Extensions["traceId"]);
            Assert.Equal("corr-tenant-me", problem.Extensions["correlationId"]);
            Assert.DoesNotContain("owner@example.com", problem.Detail, StringComparison.Ordinal);
        }

        [Fact]
        public void MeAction_UsesTenantRouteAndTenantUserAuthorization()
        {
            var controllerType = typeof(TenantMeController);
            var route = controllerType.GetCustomAttribute<RouteAttribute>();
            Assert.NotNull(route);
            Assert.Equal("t/{slug}/api/v1/me", route!.Template);

            var action = controllerType.GetMethod(nameof(TenantMeController.Me))!;
            Assert.NotNull(action.GetCustomAttribute<HttpGetAttribute>());
            var authorize = action.GetCustomAttribute<TenantAuthorizeAttribute>();
            Assert.NotNull(authorize);
            Assert.Equal(TenantPolicies.TenantUser, authorize!.Policy);
            Assert.Equal(TenantAuthenticationSchemes.Bearer, authorize.AuthenticationSchemes);
            Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
            Assert.Null(controllerType.GetCustomAttribute<AllowAnonymousAttribute>());
        }

        [Fact]
        public void TheController_RequiresTheSender()
        {
            Assert.Throws<ArgumentNullException>(() => new TenantMeController(null!));
        }
    }
}
