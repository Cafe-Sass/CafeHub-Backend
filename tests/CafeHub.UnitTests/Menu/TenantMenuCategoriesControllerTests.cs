using System.Reflection;
using System.Security.Claims;
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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;

namespace CafeHub.UnitTests.Menu
{
    public sealed class TenantMenuCategoriesControllerTests
    {
        private static readonly Guid Id = Guid.NewGuid();

        [Theory]
        [InlineData(nameof(TenantMenuCategoriesController.Create), "POST", null, TenantPermissions.MenuManage)]
        [InlineData(nameof(TenantMenuCategoriesController.GetTree), "GET", null, TenantPolicies.TenantUser)]
        [InlineData(nameof(TenantMenuCategoriesController.Get), "GET", "{id:guid}", TenantPolicies.TenantUser)]
        [InlineData(nameof(TenantMenuCategoriesController.Update), "PUT", "{id:guid}", TenantPermissions.MenuManage)]
        [InlineData(nameof(TenantMenuCategoriesController.Deactivate), "POST", "{id:guid}/deactivate", TenantPermissions.MenuManage)]
        [InlineData(nameof(TenantMenuCategoriesController.Reactivate), "POST", "{id:guid}/reactivate", TenantPermissions.MenuManage)]
        public void RoutesDeclareTheirMethodAndTenantPolicy(string action, string verb, string? template, string policy)
        {
            Assert.Equal("t/{slug}/api/v1/menu/categories", typeof(TenantMenuCategoriesController).GetCustomAttribute<RouteAttribute>()!.Template);
            var method = typeof(TenantMenuCategoriesController).GetMethod(action)!;
            var route = Assert.Single(method.GetCustomAttributes<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>());
            Assert.Equal(verb, Assert.Single(route.HttpMethods));
            Assert.Equal(template, route.Template);
            var access = method.GetCustomAttribute<TenantAuthorizeAttribute>()!;
            Assert.Equal(policy, access.Policy);
            Assert.Equal(TenantAuthenticationSchemes.Bearer, access.AuthenticationSchemes);
        }

        [Fact]
        public void ApiExplorer_DescribesTheRuntimeResponseTypesForAllSixActions()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddControllers().AddApplicationPart(typeof(TenantMenuCategoriesController).Assembly);
            using var provider = services.BuildServiceProvider();
            var actions = provider.GetRequiredService<IApiDescriptionGroupCollectionProvider>()
                .ApiDescriptionGroups.Items.SelectMany(group => group.Items)
                .Where(description => description.ActionDescriptor is ControllerActionDescriptor action
                    && action.ControllerTypeInfo.AsType() == typeof(TenantMenuCategoriesController))
                .ToDictionary(description => ((ControllerActionDescriptor)description.ActionDescriptor).ActionName);

            Assert.Equal(6, actions.Count);
            Verify(nameof(TenantMenuCategoriesController.Create), typeof(MenuCategoryResponse), 201, 400, 401, 403, 404, 409);
            Verify(nameof(TenantMenuCategoriesController.GetTree), typeof(OffsetPage<MenuCategoryTreeNodeResponse>), 200, 400, 401, 403, 404);
            Verify(nameof(TenantMenuCategoriesController.Get), typeof(MenuCategoryResponse), 200, 400, 401, 404);
            Verify(nameof(TenantMenuCategoriesController.Update), typeof(MenuCategoryResponse), 200, 400, 401, 403, 404, 409);
            Verify(nameof(TenantMenuCategoriesController.Deactivate), typeof(MenuCategoryResponse), 200, 400, 401, 403, 404, 409);
            Verify(nameof(TenantMenuCategoriesController.Reactivate), typeof(MenuCategoryResponse), 200, 400, 401, 403, 404, 409);

            void Verify(string action, Type successType, params int[] codes)
            {
                var responses = actions[action].SupportedResponseTypes.OrderBy(response => response.StatusCode).ToArray();
                Assert.Equal(codes, responses.Select(response => response.StatusCode));
                Assert.Equal(successType, responses[0].Type);
                Assert.Equal("application/json", Assert.Single(responses[0].ApiResponseFormats).MediaType);
                Assert.All(responses.Skip(1), response =>
                {
                    Assert.Equal(response.StatusCode == 400 ? typeof(ValidationProblemDetails) : typeof(ProblemDetails), response.Type);
                    Assert.Contains(response.ApiResponseFormats, format => format.MediaType == "application/problem+json");
                });
            }
        }

        [Fact]
        public async Task Create_ReturnsLocationForTheNewCategory()
        {
            var sender = new RecordingSender { Response = Result.Success(new MenuCategoryResponse(Id, null, "Coffee", 0, "Active", "13")) };
            var controller = CreateController(sender, new PolicyStub(false));

            var response = await controller.Create("fixture-cafe", new CreateMenuCategoryRequest("Coffee", 0), default);

            var created = Assert.IsType<CreatedAtActionResult>(response.Result);
            Assert.Equal(nameof(TenantMenuCategoriesController.Get), created.ActionName);
            Assert.Equal(Id, created.RouteValues!["id"]);
            Assert.Equal("fixture-cafe", created.RouteValues["slug"]);
            Assert.IsType<CreateMenuCategoryCommand>(sender.LastRequest);
        }

        [Fact]
        public void MissingSortOrderIsRejectedWhileExplicitZeroIsAccepted()
        {
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CreateMenuCategoryRequest>("""{"name":"Coffee"}""", options));
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<UpdateMenuCategoryRequest>("""{"name":"Coffee","concurrencyToken":"1"}""", options));
            Assert.Equal(0, JsonSerializer.Deserialize<CreateMenuCategoryRequest>("""{"name":"Coffee","sortOrder":0}""", options)!.SortOrder);
        }

        [Theory]
        [InlineData(false, false, false)]
        [InlineData(true, true, false)]
        [InlineData(true, false, true)]
        public async Task Tree_OnlyDispatchesAnAuthorizedExpansion(bool includeInactive, bool allowed, bool forbidden)
        {
            var sender = new RecordingSender
            {
                Response = Result.Success(OffsetPage<MenuCategoryTreeNodeResponse>.Create([], 1, 10, 0)),
            };
            var authorization = new PolicyStub(allowed);
            var controller = CreateController(sender, authorization);
            var paging = new OffsetPageRequest { PageSize = 10, IncludeTotalCount = true };

            var response = await controller.GetTree(paging, default, includeInactive);

            Assert.Equal(includeInactive ? 1 : 0, authorization.Checks);
            if (forbidden)
            {
                Assert.Equal(TenantAuthenticationSchemes.Bearer, Assert.Single(Assert.IsType<ForbidResult>(response.Result).AuthenticationSchemes));
                Assert.Equal(0, sender.CallCount);
            }
            else
            {
                Assert.IsType<OkObjectResult>(response.Result);
                var query = Assert.IsType<GetMenuCategoryTreeQuery>(sender.LastRequest);
                Assert.Equal(includeInactive, query.IncludeInactive);
                Assert.Equal(paging, query.Paging);
            }
        }

        [Fact]
        public async Task DirectReadUsesVisibleOnlyQueryEvenForAnOwner()
        {
            var sender = new RecordingSender { Response = Result.Failure<MenuCategoryResponse>(MenuCategoryErrors.NotFound()) };
            var authorization = new PolicyStub(true);
            var controller = CreateController(sender, authorization);
            controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.Role, TenantRoles.Owner)], "fixture"));

            var result = await controller.Get(Id, default);

            Assert.Equal(404, Assert.IsType<ObjectResult>(result.Result).StatusCode);
            Assert.Equal(new GetMenuCategoryQuery(Id), Assert.IsType<GetMenuCategoryQuery>(sender.LastRequest));
            Assert.Equal(0, authorization.Checks);
        }

        private static TenantMenuCategoriesController CreateController(RecordingSender sender, PolicyStub policy) =>
            new(sender, policy, new CorrelationStub()) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };

        private sealed class CorrelationStub : ICorrelationIdAccessor
        {
            public string? CorrelationId => null;
        }

        private sealed class PolicyStub(bool allowed) : IAuthorizationService
        {
            public int Checks { get; private set; }

            public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName)
            {
                Assert.IsType<DefaultHttpContext>(resource);
                Assert.Equal(TenantPermissions.MenuManage, policyName);
                ++Checks;
                return Task.FromResult(allowed ? AuthorizationResult.Success() : AuthorizationResult.Failed());
            }

            public Task<AuthorizationResult> AuthorizeAsync(
                ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements) =>
                throw new NotSupportedException();
        }
    }
}
