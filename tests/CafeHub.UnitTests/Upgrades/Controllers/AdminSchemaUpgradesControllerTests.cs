using System.Collections.Generic;
using System.Reflection;
using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Upgrades;
using CafeHub.Application.Upgrades.Commands;
using CafeHub.Application.Upgrades.DTOs;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Controllers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.UnitTests.Upgrades.Controllers
{
    public sealed class AdminSchemaUpgradesControllerTests
    {
        private const string TraceIdentifier = "trace-admin-schema-upgrades";

        private static AdminSchemaUpgradesController CreateController(
            ISender sender,
            ICorrelationIdAccessor? correlationIdAccessor = null)
        {
            return new AdminSchemaUpgradesController(sender, correlationIdAccessor)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { TraceIdentifier = TraceIdentifier },
                },
            };
        }

        private static MethodInfo Action(string name)
        {
            return typeof(AdminSchemaUpgradesController).GetMethod(name, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"The AdminSchemaUpgradesController.{name} action was not found.");
        }

        [Fact]
        public void AdminSchemaUpgradesController_IsAnApiControllerOnTheDocumentedRoute()
        {
            Assert.True(typeof(ControllerBase).IsAssignableFrom(typeof(AdminSchemaUpgradesController)));
            Assert.NotNull(typeof(AdminSchemaUpgradesController).GetCustomAttribute<ApiControllerAttribute>());
            var route = typeof(AdminSchemaUpgradesController).GetCustomAttribute<RouteAttribute>();
            Assert.NotNull(route);
            Assert.Equal("api/v1/admin/tenants/{tenantId:guid}/schema-upgrades", route!.Template);
        }

        [Fact]
        public void QueueAction_IsDeclaredOnPostAndRequiresSuperAdmin()
        {
            var queue = Action(nameof(AdminSchemaUpgradesController.Queue));
            var post = queue.GetCustomAttribute<HttpPostAttribute>();
            Assert.NotNull(post);
            Assert.Null(post!.Template);
            var authorize = queue.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(authorize);
            Assert.Equal(PlatformAuthorizationPolicies.SuperAdmin, authorize!.Policy);
            Assert.Null(queue.GetCustomAttribute<AllowAnonymousAttribute>());
            Assert.Null(typeof(AdminSchemaUpgradesController).GetCustomAttribute<AllowAnonymousAttribute>());
        }

        [Fact]
        public async Task Queue_SendsTheCommandWithTheRouteTenantIdAndReturnsOk()
        {
            var response = new TenantSchemaUpgradeResponse(
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                "Pending",
                "latest",
                Guid.CreateVersion7(),
                new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero),
                Guid.CreateVersion7());
            var sender = new RecordingSender { Response = Result.Success(response) };
            var controller = CreateController(sender);
            var tenantId = Guid.CreateVersion7();

            var actionResult = await controller.Queue(tenantId, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            Assert.Same(response, okResult.Value);
            var command = Assert.IsType<QueueTenantSchemaUpgradeCommand>(sender.LastRequest);
            Assert.Equal(tenantId, command.TenantKey);
            Assert.Equal(CancellationToken.None, sender.LastCancellationToken);
        }

        [Fact]
        public async Task Queue_WhenTheQueueFails_MapsTheProblemDetails()
        {
            var sender = new RecordingSender
            {
                Response = Result.Failure<TenantSchemaUpgradeResponse>(TenantSchemaUpgradeErrors.TenantNotFound()),
            };
            var controller = CreateController(sender);

            var actionResult = await controller.Queue(Guid.CreateVersion7(), CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);
            Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
            var problemDetails = Assert.IsType<ProblemDetails>(objectResult.Value);
            Assert.Equal(StatusCodes.Status404NotFound, problemDetails.Status);
            Assert.Equal(TenantSchemaUpgradeErrors.TenantNotFoundCode, problemDetails.Extensions["code"]);
            Assert.Equal(TenantSchemaUpgradeErrors.TenantNotFoundMessage, problemDetails.Detail);
            Assert.Equal(TraceIdentifier, problemDetails.Extensions["traceId"]);
        }

        [Fact]
        public void TheController_RequiresTheSender()
        {
            Assert.Throws<ArgumentNullException>(() => new AdminSchemaUpgradesController(null!));
        }

        private sealed class RecordingSender : ISender
        {
            public object? Response { get; set; }
            public object? LastRequest { get; private set; }
            public CancellationToken LastCancellationToken { get; private set; }

            public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
            {
                LastRequest = request;
                LastCancellationToken = cancellationToken;
                return Task.FromResult((TResponse)Response!);
            }

            public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
            {
                throw new NotSupportedException("The controller must send a request with a response.");
            }

            public Task<object?> Send(object request, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException("The controller must send a typed request.");
            }

            public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException("The controller does not create streams.");
            }

            public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException("The controller does not create streams.");
            }
        }
    }
}
