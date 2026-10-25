using System.Reflection;
using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Pagination;
using CafeHub.Application.Plans;
using CafeHub.Application.Plans.Commands;
using CafeHub.Application.Plans.DTOs;
using CafeHub.Application.Plans.Queries;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Controllers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.UnitTests.Plans.Controllers
{
    public sealed class AdminPlansControllerTests
    {
        private const string TraceIdentifier = "trace-admin-plans";

        private static AdminPlansController CreateController(
            ISender sender,
            ICorrelationIdAccessor? correlationIdAccessor = null)
        {
            return new AdminPlansController(sender, correlationIdAccessor)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { TraceIdentifier = TraceIdentifier },
                },
            };
        }

        private static PlanResponse CreatePlanResponse()
        {
            return new PlanResponse
            {
                Id = Guid.NewGuid(),
                Key = "starter",
                Name = "Starter",
                IsSelectable = true,
                Versions = Array.Empty<PlanVersionResponse>(),
                CreatedAt = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero),
                UpdatedAt = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero),
            };
        }

        private static MethodInfo Action(string name)
        {
            return typeof(AdminPlansController).GetMethod(name, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"The AdminPlansController.{name} action was not found.");
        }

        private static MethodInfo[] ActionMethods()
        {
            return typeof(AdminPlansController)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method =>
                    method.GetCustomAttribute<HttpGetAttribute>() is not null
                    || method.GetCustomAttribute<HttpPostAttribute>() is not null)
                .ToArray();
        }

        [Fact]
        public void AdminPlansController_IsAnApiControllerOnTheDocumentedRoute()
        {
            Assert.True(typeof(ControllerBase).IsAssignableFrom(typeof(AdminPlansController)));
            Assert.NotNull(typeof(AdminPlansController).GetCustomAttribute<ApiControllerAttribute>());
            var route = typeof(AdminPlansController).GetCustomAttribute<RouteAttribute>();
            Assert.NotNull(route);
            Assert.Equal("api/v1/admin/plans", route!.Template);
        }

        [Fact]
        public void Actions_AreDeclaredOnTheDocumentedVerbsAndTemplates()
        {
            var create = Action(nameof(AdminPlansController.Create));
            var createPost = create.GetCustomAttribute<HttpPostAttribute>();
            Assert.NotNull(createPost);
            Assert.Null(createPost!.Template);

            var createVersion = Action(nameof(AdminPlansController.CreateVersion));
            var createVersionPost = createVersion.GetCustomAttribute<HttpPostAttribute>();
            Assert.NotNull(createVersionPost);
            Assert.Equal("{planId:guid}/versions", createVersionPost!.Template);

            var list = Action(nameof(AdminPlansController.List));
            var listGet = list.GetCustomAttribute<HttpGetAttribute>();
            Assert.NotNull(listGet);
            Assert.Null(listGet!.Template);

            var get = Action(nameof(AdminPlansController.Get));
            var getGet = get.GetCustomAttribute<HttpGetAttribute>();
            Assert.NotNull(getGet);
            Assert.Equal("{planId:guid}", getGet!.Template);
        }

        [Fact]
        public void EveryAction_RequiresTheSuperAdminPolicyAndNoActionIsAnonymous()
        {
            var actions = ActionMethods();

            Assert.Equal(4, actions.Length);
            foreach (var action in actions)
            {
                var authorize = action.GetCustomAttribute<AuthorizeAttribute>();
                Assert.NotNull(authorize);
                Assert.Equal(PlatformAuthorizationPolicies.SuperAdmin, authorize!.Policy);
                Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
            }

            Assert.Null(typeof(AdminPlansController).GetCustomAttribute<AllowAnonymousAttribute>());
        }

        [Fact]
        public async Task Create_SendsTheCommandAndReturnsCreatedAtThePlanDetail()
        {
            var response = CreatePlanResponse();
            var sender = new RecordingSender { Response = Result.Success(response) };
            var controller = CreateController(sender);
            var command = new CreatePlanCommand("starter", "Starter", true);
            using var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            var actionResult = await controller.Create(command, cancellationToken);

            var created = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
            Assert.Equal(nameof(AdminPlansController.Get), created.ActionName);
            Assert.NotNull(created.RouteValues);
            Assert.Equal(response.Id, created.RouteValues!["planId"]);
            Assert.Same(response, created.Value);
            Assert.Same(command, sender.LastRequest);
            Assert.Equal(cancellationToken, sender.LastCancellationToken);
        }

        [Fact]
        public async Task Create_WhenTheCommandFails_MapsTheConflictProblemDetails()
        {
            var sender = new RecordingSender
            {
                Response = Result.Failure<PlanResponse>(PlansErrors.KeyAlreadyExists()),
            };
            var controller = CreateController(sender, new FixedCorrelationIdAccessor("corr-plans-create"));

            var actionResult = await controller.Create(
                new CreatePlanCommand("starter", "Starter", true),
                CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);
            Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);
            var problemDetails = Assert.IsType<ProblemDetails>(objectResult.Value);
            Assert.Equal(StatusCodes.Status409Conflict, problemDetails.Status);
            Assert.Equal(PlansErrors.KeyAlreadyExistsCode, problemDetails.Extensions["code"]);
            Assert.Equal(PlansErrors.KeyAlreadyExistsMessage, problemDetails.Detail);
            Assert.Equal(TraceIdentifier, problemDetails.Extensions["traceId"]);
            Assert.Equal("corr-plans-create", problemDetails.Extensions["correlationId"]);
        }

        [Fact]
        public async Task CreateVersion_UsesTheRoutePlanIdAndReturnsCreatedAtThePlanDetail()
        {
            var response = CreatePlanResponse();
            var sender = new RecordingSender { Response = Result.Success(response) };
            var controller = CreateController(sender);
            var routePlanId = Guid.NewGuid();
            var bodyPlanId = Guid.NewGuid();
            var command = new CreatePlanVersionCommand(
                bodyPlanId,
                19.99m,
                "usd",
                "Monthly",
                new[] { new PlanEntitlementRequest("max_tables", "10") });

            var actionResult = await controller.CreateVersion(routePlanId, command, CancellationToken.None);

            var sent = Assert.IsType<CreatePlanVersionCommand>(sender.LastRequest);
            Assert.Equal(routePlanId, sent.PlanId);
            Assert.Equal(bodyPlanId, command.PlanId);
            var created = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
            Assert.Equal(nameof(AdminPlansController.Get), created.ActionName);
            Assert.Equal(response.Id, created.RouteValues!["planId"]);
            Assert.Same(response, created.Value);
        }

        [Fact]
        public async Task CreateVersion_WhenThePlanIsMissing_MapsTheNotFoundProblemDetails()
        {
            var sender = new RecordingSender
            {
                Response = Result.Failure<PlanResponse>(PlansErrors.NotFound()),
            };
            var controller = CreateController(sender);

            var actionResult = await controller.CreateVersion(
                Guid.NewGuid(),
                new CreatePlanVersionCommand(Guid.Empty, 19.99m, "usd", "Monthly", Array.Empty<PlanEntitlementRequest>()),
                CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);
            Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
            var problemDetails = Assert.IsType<ProblemDetails>(objectResult.Value);
            Assert.Equal(PlansErrors.NotFoundCode, problemDetails.Extensions["code"]);
        }

        [Fact]
        public async Task List_SendsTheQueryAndReturnsThePage()
        {
            var response = CreatePlanResponse();
            var page = new OffsetPage<PlanResponse>
            {
                Items = new[] { response },
                Page = 2,
                PageSize = 10,
                TotalCount = 11,
                HasNextPage = true,
            };
            var sender = new RecordingSender { Response = Result.Success(page) };
            var controller = CreateController(sender);
            var query = new ListPlansQuery
            {
                Page = 2,
                PageSize = 10,
                IncludeTotalCount = true,
                Sort = PlanListSort.KeyField,
                SortDirection = SortDirection.Desc,
            };

            var actionResult = await controller.List(query, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            Assert.Same(page, okResult.Value);
            Assert.Same(query, sender.LastRequest);
        }

        [Fact]
        public async Task List_WhenTheQueryFails_MapsTheValidationProblemDetails()
        {
            var validationError = ValidationError.Create(new[] { new FieldError("page", "The page must start at 1.") });
            var sender = new RecordingSender
            {
                Response = Result.Failure<OffsetPage<PlanResponse>>(validationError),
            };
            var controller = CreateController(sender);

            var actionResult = await controller.List(new ListPlansQuery { Page = 0 }, CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);
            Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
            var problemDetails = Assert.IsType<ValidationProblemDetails>(objectResult.Value);
            Assert.Equal(validationError.Code, problemDetails.Extensions["code"]);
            Assert.True(problemDetails.Errors.TryGetValue("page", out var messages));
            Assert.Equal(new[] { "The page must start at 1." }, messages);
        }

        [Fact]
        public async Task Get_SendsTheGetPlanQueryAndReturnsThePlan()
        {
            var response = CreatePlanResponse();
            var sender = new RecordingSender { Response = Result.Success(response) };
            var controller = CreateController(sender);
            var planId = Guid.NewGuid();

            var actionResult = await controller.Get(planId, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            Assert.Same(response, okResult.Value);
            var query = Assert.IsType<GetPlanQuery>(sender.LastRequest);
            Assert.Equal(planId, query.PlanId);
        }

        [Fact]
        public async Task Get_WhenThePlanIsMissing_MapsTheNotFoundProblemDetails()
        {
            var sender = new RecordingSender
            {
                Response = Result.Failure<PlanResponse>(PlansErrors.NotFound()),
            };
            var controller = CreateController(sender);

            var actionResult = await controller.Get(Guid.NewGuid(), CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);
            Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
            var problemDetails = Assert.IsType<ProblemDetails>(objectResult.Value);
            Assert.Equal(PlansErrors.NotFoundCode, problemDetails.Extensions["code"]);
        }

        [Fact]
        public void TheController_RequiresTheSender()
        {
            Assert.Throws<ArgumentNullException>(() => new AdminPlansController(null!));
        }
    }
}
