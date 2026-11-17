using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Menu.Commands;
using CafeHub.Application.Menu.DTOs;
using CafeHub.Application.Menu.Queries;
using CafeHub.Application.Pagination;
using CafeHub.Domain.Tenants;
using CafeHub.WebApi.Authentication;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Controllers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;

namespace CafeHub.UnitTests.Menu
{
    public sealed class TenantProductsControllerTests
    {
        private static readonly Guid Id = Guid.NewGuid();

        [Theory]
        [InlineData(nameof(TenantProductsController.Create), "POST", null, TenantPermissions.MenuManage)]
        [InlineData(nameof(TenantProductsController.List), "GET", null, TenantPolicies.TenantUser)]
        [InlineData(nameof(TenantProductsController.Get), "GET", "{id:guid}", TenantPolicies.TenantUser)]
        [InlineData(nameof(TenantProductsController.Update), "PUT", "{id:guid}", TenantPermissions.MenuManage)]
        [InlineData(nameof(TenantProductsController.Deactivate), "POST", "{id:guid}/deactivate", TenantPermissions.MenuManage)]
        [InlineData(nameof(TenantProductsController.Reactivate), "POST", "{id:guid}/reactivate", TenantPermissions.MenuManage)]
        public void RoutesDeclareTheirMethodAndTenantPolicy(string action, string verb, string? template, string policy)
        {
            Assert.Equal("t/{slug}/api/v1/menu/products", typeof(TenantProductsController).GetCustomAttribute<RouteAttribute>()!.Template);
            var method = typeof(TenantProductsController).GetMethod(action)!;
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
            services.AddControllers().AddApplicationPart(typeof(TenantProductsController).Assembly);
            using var provider = services.BuildServiceProvider();
            var actions = provider.GetRequiredService<IApiDescriptionGroupCollectionProvider>()
                .ApiDescriptionGroups.Items.SelectMany(group => group.Items)
                .Where(description => description.ActionDescriptor is ControllerActionDescriptor action
                    && action.ControllerTypeInfo.AsType() == typeof(TenantProductsController))
                .ToDictionary(description => ((ControllerActionDescriptor)description.ActionDescriptor).ActionName);

            Assert.Equal(6, actions.Count);
            Verify(nameof(TenantProductsController.Create), typeof(ProductResponse), 201, 400, 401, 403, 409);
            Verify(nameof(TenantProductsController.List), typeof(OffsetPage<ProductListItemResponse>), 200, 400, 401, 403);
            Verify(nameof(TenantProductsController.Get), typeof(ProductResponse), 200, 400, 401, 404);
            Verify(nameof(TenantProductsController.Update), typeof(ProductResponse), 200, 400, 401, 403, 404, 409);
            Verify(nameof(TenantProductsController.Deactivate), typeof(ProductResponse), 200, 400, 401, 403, 404, 409);
            Verify(nameof(TenantProductsController.Reactivate), typeof(ProductResponse), 200, 400, 401, 403, 404, 409);

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
        public async Task Create_ReturnsLocationForTheNewProduct()
        {
            var sender = new RecordingSender { Response = Result.Success(Response(Id)) };
            var controller = CreateController(sender, new PolicyStub(true));

            var response = await controller.Create("fixture-cafe", new CreateProductRequest(
                Guid.NewGuid(), null, "Latte", null, null, "Drink", true, null, 0, [], [], []), default);

            var created = Assert.IsType<CreatedAtActionResult>(response.Result);
            Assert.Equal(nameof(TenantProductsController.Get), created.ActionName);
            Assert.Equal(Id, created.RouteValues!["id"]);
            Assert.Equal("fixture-cafe", created.RouteValues["slug"]);
            Assert.IsType<CreateProductCommand>(sender.LastRequest);
        }

        [Fact]
        public async Task List_ReadOnlyCallerAlwaysRequestsActiveProducts()
        {
            var sender = new RecordingSender
            {
                Response = Result.Success(OffsetPage<ProductListItemResponse>.Create([], 1, 10, null)),
            };
            var controller = CreateController(sender, new PolicyStub(false));

            var result = await controller.List("fixture-cafe",
                new ListProductsQuery { Page = 1, PageSize = 10, Status = ProductStatus.Inactive }, default);

            Assert.IsType<OkObjectResult>(result.Result);
            var query = Assert.IsType<ListProductsQuery>(sender.LastRequest);
            Assert.Equal(ProductStatus.Active, query.Status);
        }

        [Fact]
        public async Task Get_InactiveProductReturnsNotFoundForReadOnlyCaller()
        {
            var sender = new RecordingSender
            {
                Response = Result.Success(Response(Id, nameof(ProductStatus.Inactive))),
            };
            var controller = CreateController(sender, new PolicyStub(false));

            var result = await controller.Get("fixture-cafe", Id, default);

            Assert.Equal(404, Assert.IsType<ObjectResult>(result.Result).StatusCode);
        }

        [Fact]
        public async Task Get_ReturnsTheProductForAReadOnlyCallerWhenActive()
        {
            var sender = new RecordingSender
            {
                Response = Result.Success(Response(Id, nameof(ProductStatus.Active))),
            };
            var controller = CreateController(sender, new PolicyStub(false));

            var result = await controller.Get("fixture-cafe", Id, default);

            Assert.IsType<OkObjectResult>(result.Result);
        }

        [Fact]
        public async Task MissingConcurrencyTokenIsRejectedForLifecycleActions()
        {
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ProductLifecycleRequest>("""{}""", options));
        }

        private static TenantProductsController CreateController(RecordingSender sender, PolicyStub policy) =>
            new(sender, policy, new CorrelationStub()) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };

        private static ProductResponse Response(Guid? id = null, string? status = null) => new()
        {
            Id = id ?? Guid.NewGuid(),
            CategoryId = Guid.NewGuid(),
            StationId = null,
            Name = "Latte",
            Description = null,
            Ingredients = null,
            Kind = nameof(ProductKind.Drink),
            Caffeine = true,
            PreparationMinutes = null,
            SortOrder = 0,
            Status = status ?? nameof(ProductStatus.Active),
            Sizes = [],
            Addons = [],
            Tags = [],
            CreatedAt = DateTimeOffset.UnixEpoch,
            UpdatedAt = DateTimeOffset.UnixEpoch,
            ConcurrencyToken = "1",
        };

        private sealed class CorrelationStub : ICorrelationIdAccessor
        {
            public string? CorrelationId => null;
        }

        private sealed class RecordingSender : ISender
        {
            public List<object> Requests { get; } = new();
            public object? LastRequest => Requests.Count > 0 ? Requests[^1] : null;
            public int CallCount => Requests.Count;
            public object? Response { get; set; }

            public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
            {
                Requests.Add(request);
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

        private sealed class PolicyStub(bool allowed) : IAuthorizationService
        {
            public List<string> Policies { get; } = new();

            public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName)
            {
                Policies.Add(policyName);
                Assert.IsType<DefaultHttpContext>(resource);
                return Task.FromResult(allowed ? AuthorizationResult.Success() : AuthorizationResult.Failed());
            }

            public Task<AuthorizationResult> AuthorizeAsync(
                ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements) =>
                throw new NotSupportedException();
        }
    }
}
