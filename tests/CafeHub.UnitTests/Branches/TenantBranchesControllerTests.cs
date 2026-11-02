using System.Reflection;
using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Branches;
using CafeHub.Application.Branches.Commands;
using CafeHub.Application.Branches.DTOs;
using CafeHub.Application.Branches.Queries;
using CafeHub.Application.Pagination;
using CafeHub.WebApi.Authentication;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Controllers;
using CafeHub.WebApi.Errors;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.UnitTests.Branches
{
    public sealed class TenantBranchesControllerTests
    {
        private const string TraceIdentifier = "trace-branches";

        private static TenantBranchesController CreateController(
            ISender sender,
            ICorrelationIdAccessor? correlationIdAccessor = null)
        {
            return new TenantBranchesController(sender, correlationIdAccessor)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { TraceIdentifier = TraceIdentifier },
                },
            };
        }

        private static BranchResponse CreateBranchResponse()
        {
            return new BranchResponse
            {
                Id = Guid.NewGuid(),
                Name = "Downtown Cafe",
                LifecycleStatus = "Active",
                Address = "123 Main St",
                ContactPhone = "+1-555-0100",
                OpeningHoursSummary = "Mon-Fri 8am-6pm",
                CreatedAt = new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero),
                UpdatedAt = new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero),
                ConcurrencyToken = "12345",
            };
        }

        private static MethodInfo Action(string name)
        {
            return typeof(TenantBranchesController).GetMethod(name, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"The TenantBranchesController.{name} action was not found.");
        }

        [Fact]
        public void TenantBranchesController_IsAnApiControllerOnTheDocumentedRoute()
        {
            Assert.True(typeof(ControllerBase).IsAssignableFrom(typeof(TenantBranchesController)));
            Assert.NotNull(typeof(TenantBranchesController).GetCustomAttribute<ApiControllerAttribute>());
            var route = typeof(TenantBranchesController).GetCustomAttribute<RouteAttribute>();
            Assert.NotNull(route);
            Assert.Equal("t/{slug}/api/v1/branches", route!.Template);
        }

        [Fact]
        public void Actions_AreDeclaredOnTheDocumentedVerbsAndTemplates()
        {
            var list = Action(nameof(TenantBranchesController.List));
            var listGet = list.GetCustomAttribute<HttpGetAttribute>();
            Assert.NotNull(listGet);
            Assert.Null(listGet!.Template);

            var get = Action(nameof(TenantBranchesController.Get));
            var getGet = get.GetCustomAttribute<HttpGetAttribute>();
            Assert.NotNull(getGet);
            Assert.Equal("{branchId:guid}", getGet!.Template);

            var create = Action(nameof(TenantBranchesController.Create));
            var createPost = create.GetCustomAttribute<HttpPostAttribute>();
            Assert.NotNull(createPost);
            Assert.Null(createPost!.Template);

            var put = Action(nameof(TenantBranchesController.Put));
            var putHttpPut = put.GetCustomAttribute<HttpPutAttribute>();
            Assert.NotNull(putHttpPut);
            Assert.Equal("{branchId:guid}", putHttpPut!.Template);

            var suspend = Action(nameof(TenantBranchesController.Suspend));
            var suspendPost = suspend.GetCustomAttribute<HttpPostAttribute>();
            Assert.NotNull(suspendPost);
            Assert.Equal("{branchId:guid}/suspend", suspendPost!.Template);

            var reactivate = Action(nameof(TenantBranchesController.Reactivate));
            var reactivatePost = reactivate.GetCustomAttribute<HttpPostAttribute>();
            Assert.NotNull(reactivatePost);
            Assert.Equal("{branchId:guid}/reactivate", reactivatePost!.Template);

            var retire = Action(nameof(TenantBranchesController.Retire));
            var retirePost = retire.GetCustomAttribute<HttpPostAttribute>();
            Assert.NotNull(retirePost);
            Assert.Equal("{branchId:guid}/retire", retirePost!.Template);
        }

        [Fact]
        public void ReadActions_RequireTenantUserPolicy()
        {
            foreach (var name in new[] { nameof(TenantBranchesController.List), nameof(TenantBranchesController.Get) })
            {
                var action = Action(name);
                var authorize = action.GetCustomAttribute<TenantAuthorizeAttribute>();
                Assert.NotNull(authorize);
                Assert.Equal(TenantPolicies.TenantUser, authorize!.Policy);
                Assert.Equal(TenantAuthenticationSchemes.Bearer, authorize.AuthenticationSchemes);
                Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
            }

            Assert.Null(typeof(TenantBranchesController).GetCustomAttribute<AllowAnonymousAttribute>());
        }

        [Fact]
        public void WriteAndLifecycleActions_RequireTenantOwnerPolicy()
        {
            foreach (var name in new[]
            {
                nameof(TenantBranchesController.Create),
                nameof(TenantBranchesController.Put),
                nameof(TenantBranchesController.Suspend),
                nameof(TenantBranchesController.Reactivate),
                nameof(TenantBranchesController.Retire),
            })
            {
                var action = Action(name);
                var authorize = action.GetCustomAttribute<TenantAuthorizeAttribute>();
                Assert.NotNull(authorize);
                Assert.Equal(TenantPolicies.TenantOwner, authorize!.Policy);
                Assert.Equal(TenantAuthenticationSchemes.Bearer, authorize.AuthenticationSchemes);
                Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
            }
        }

        [Fact]
        public async Task Create_SendsCreateBranchCommandAndReturnsCreatedAtBranchDetail()
        {
            var response = CreateBranchResponse();
            var sender = new RecordingSender { Response = Result.Success(response) };
            var controller = CreateController(sender, new TestCorrelationIdAccessor("corr-branches-create"));
            var request = new CreateBranchRequest("Downtown Cafe", "123 Main St", "+1-555-0100", "Mon-Fri 8am-6pm");

            var actionResult = await controller.Create(request, CancellationToken.None);

            var created = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
            Assert.Equal(nameof(TenantBranchesController.Get), created.ActionName);
            Assert.NotNull(created.RouteValues);
            Assert.Equal(response.Id, created.RouteValues!["branchId"]);
            Assert.Same(response, created.Value);
            var command = Assert.IsType<CreateBranchCommand>(sender.LastRequest);
            Assert.Equal("Downtown Cafe", command.Name);
        }

        [Fact]
        public async Task Create_WhenValidationFails_MapsTo400()
        {
            var validationError = ValidationError.Create(new[] { new FieldError("Name", "The branch name is required.") });
            var sender = new RecordingSender { Response = Result.Failure<BranchResponse>(validationError) };
            var controller = CreateController(sender);

            var actionResult = await controller.Create(new CreateBranchRequest("", null, null, null), CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);
            Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
            var problemDetails = Assert.IsType<ValidationProblemDetails>(objectResult.Value);
            Assert.Equal(validationError.Code, problemDetails.Extensions["code"]);
        }

        [Fact]
        public async Task Put_SendsUpdateBranchCommandAndReturnsBranch()
        {
            var response = CreateBranchResponse();
            var sender = new RecordingSender { Response = Result.Success(response) };
            var controller = CreateController(sender);
            var branchId = Guid.NewGuid();
            var request = new UpdateBranchRequest("Downtown Cafe West", "456 Side St", "+1-555-0101", "Mon-Fri 9am-7pm", "12345");

            var actionResult = await controller.Put(branchId, request, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            Assert.Same(response, okResult.Value);
            var command = Assert.IsType<UpdateBranchCommand>(sender.LastRequest);
            Assert.Equal(branchId, command.BranchId);
            Assert.Equal("Downtown Cafe West", command.Name);
        }

        [Fact]
        public async Task Put_WhenNotFound_MapsTo404()
        {
            var sender = new RecordingSender { Response = Result.Failure<BranchResponse>(BranchesErrors.NotFound()) };
            var controller = CreateController(sender);

            var actionResult = await controller.Put(
                Guid.NewGuid(),
                new UpdateBranchRequest("Downtown Cafe", null, null, null, "12345"),
                CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);
            Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
            var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
            Assert.Equal(BranchesErrors.NotFoundCode, problem.Extensions["code"]);
        }

        [Fact]
        public async Task Put_WhenConcurrencyConflict_MapsTo409()
        {
            var sender = new RecordingSender { Response = Result.Failure<BranchResponse>(BranchesErrors.ConcurrencyConflict()) };
            var controller = CreateController(sender);

            var actionResult = await controller.Put(
                Guid.NewGuid(),
                new UpdateBranchRequest("Downtown Cafe", null, null, null, "99999"),
                CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);
            Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);
            var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
            Assert.Equal(BranchesErrors.ConcurrencyConflictCode, problem.Extensions["code"]);
        }

        [Fact]
        public async Task Suspend_SendsSuspendBranchCommandAndReturnsBranch()
        {
            var response = CreateBranchResponse();
            var sender = new RecordingSender { Response = Result.Success(response) };
            var controller = CreateController(sender);
            var branchId = Guid.NewGuid();

            var actionResult = await controller.Suspend(branchId, new BranchLifecycleRequest("12345"), CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            Assert.Same(response, okResult.Value);
            var command = Assert.IsType<SuspendBranchCommand>(sender.LastRequest);
            Assert.Equal(branchId, command.BranchId);
            Assert.Equal("12345", command.ConcurrencyToken);
        }

        [Fact]
        public async Task Reactivate_SendsReactivateBranchCommandAndReturnsBranch()
        {
            var response = CreateBranchResponse();
            var sender = new RecordingSender { Response = Result.Success(response) };
            var controller = CreateController(sender);
            var branchId = Guid.NewGuid();

            var actionResult = await controller.Reactivate(branchId, new BranchLifecycleRequest("12345"), CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            Assert.Same(response, okResult.Value);
            var command = Assert.IsType<ReactivateBranchCommand>(sender.LastRequest);
            Assert.Equal(branchId, command.BranchId);
            Assert.Equal("12345", command.ConcurrencyToken);
        }

        [Fact]
        public async Task Retire_SendsRetireBranchCommandAndReturnsBranch()
        {
            var response = CreateBranchResponse();
            var sender = new RecordingSender { Response = Result.Success(response) };
            var controller = CreateController(sender);
            var branchId = Guid.NewGuid();

            var actionResult = await controller.Retire(branchId, new BranchLifecycleRequest("12345"), CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            Assert.Same(response, okResult.Value);
            var command = Assert.IsType<RetireBranchCommand>(sender.LastRequest);
            Assert.Equal(branchId, command.BranchId);
            Assert.Equal("12345", command.ConcurrencyToken);
        }

        [Fact]
        public async Task List_SendsListBranchesQueryAndReturnsPage()
        {
            var response = CreateBranchResponse();
            var page = new OffsetPage<BranchResponse>
            {
                Items = new[] { response },
                Page = 1,
                PageSize = 20,
                TotalCount = 1,
                HasNextPage = false,
            };
            var sender = new RecordingSender { Response = Result.Success(page) };
            var controller = CreateController(sender);
            var query = new ListBranchesQuery
            {
                Page = 1,
                PageSize = 20,
                IncludeTotalCount = true,
                Sort = "name",
                SortDirection = SortDirection.Asc,
                Status = null,
            };

            var actionResult = await controller.List(query, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            Assert.Same(page, okResult.Value);
            Assert.Same(query, sender.LastRequest);
        }

        [Fact]
        public void TheController_RequiresTheSender()
        {
            Assert.Throws<ArgumentNullException>(() => new TenantBranchesController(null!));
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

        private sealed class TestCorrelationIdAccessor(string? correlationId) : ICorrelationIdAccessor
        {
            public TestCorrelationIdAccessor() : this(null) { }

            public string? CorrelationId { get; } = correlationId;
        }
    }
}
