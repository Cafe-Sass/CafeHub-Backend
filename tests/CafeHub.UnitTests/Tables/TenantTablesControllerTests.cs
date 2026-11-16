using System.Reflection;
using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Pagination;
using CafeHub.Application.Tables;
using CafeHub.Application.Tables.Commands;
using CafeHub.Application.Tables.DTOs;
using CafeHub.Application.Tables.Queries;
using CafeHub.Application.Tenancy;
using CafeHub.Infrastructure.Tenancy;
using CafeHub.WebApi.Authentication;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Controllers;
using CafeHub.WebApi.Errors;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.UnitTests.Tables
{
    public sealed class TenantTablesControllerTests
    {
        private const string TraceIdentifier = "trace-tables";

        private static TenantTablesController CreateController(
            ISender sender,
            ICurrentTenantUser currentTenantUser,
            ITenantPermissionReader permissions,
            ICorrelationIdAccessor? correlationIdAccessor = null)
        {
            return new TenantTablesController(sender, currentTenantUser, permissions, correlationIdAccessor)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { TraceIdentifier = TraceIdentifier },
                },
            };
        }

        private static TableResponse CreateTableResponse() =>
            new()
            {
                Id = Guid.NewGuid(),
                BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                Number = 3,
                Label = "Terrace 3",
                Status = "Active",
                QrCode = "Abcdefghijklmnopqrstuv",
                CreatedAt = new DateTimeOffset(2026, 11, 15, 0, 0, 0, TimeSpan.Zero),
                UpdatedAt = new DateTimeOffset(2026, 11, 15, 0, 0, 0, TimeSpan.Zero),
                ConcurrencyToken = "12345",
            };

        private static MethodInfo Action(string name) =>
            typeof(TenantTablesController).GetMethod(name, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"The TenantTablesController.{name} action was not found.");

        [Fact]
        public void TenantTablesController_IsAnApiControllerOnTheDocumentedRoute()
        {
            Assert.True(typeof(ControllerBase).IsAssignableFrom(typeof(TenantTablesController)));
            Assert.NotNull(typeof(TenantTablesController).GetCustomAttribute<ApiControllerAttribute>());
            var route = typeof(TenantTablesController).GetCustomAttribute<RouteAttribute>();
            Assert.NotNull(route);
            Assert.Equal("t/{slug}/api/v1/branches/{branchId:guid}/tables", route!.Template);
        }

        [Fact]
        public void Actions_AreDeclaredOnTheDocumentedVerbsAndTemplates()
        {
            var create = Action(nameof(TenantTablesController.Create));
            var createPost = create.GetCustomAttribute<HttpPostAttribute>();
            Assert.NotNull(createPost);
            Assert.Null(createPost!.Template);

            var bulkAdd = Action(nameof(TenantTablesController.BulkAdd));
            var bulkAddPost = bulkAdd.GetCustomAttribute<HttpPostAttribute>();
            Assert.NotNull(bulkAddPost);
            Assert.Equal("bulk-add", bulkAddPost!.Template);

            var bulkDeactivate = Action(nameof(TenantTablesController.BulkDeactivateHighest));
            var bulkDeactivatePost = bulkDeactivate.GetCustomAttribute<HttpPostAttribute>();
            Assert.NotNull(bulkDeactivatePost);
            Assert.Equal("bulk-deactivate-highest", bulkDeactivatePost!.Template);

            var list = Action(nameof(TenantTablesController.List));
            var listGet = list.GetCustomAttribute<HttpGetAttribute>();
            Assert.NotNull(listGet);
            Assert.Null(listGet!.Template);

            var get = Action(nameof(TenantTablesController.Get));
            var getGet = get.GetCustomAttribute<HttpGetAttribute>();
            Assert.NotNull(getGet);
            Assert.Equal("{tableId:guid}", getGet!.Template);

            var put = Action(nameof(TenantTablesController.Put));
            var putHttpPut = put.GetCustomAttribute<HttpPutAttribute>();
            Assert.NotNull(putHttpPut);
            Assert.Equal("{tableId:guid}", putHttpPut!.Template);

            var deactivate = Action(nameof(TenantTablesController.Deactivate));
            var deactivatePost = deactivate.GetCustomAttribute<HttpPostAttribute>();
            Assert.NotNull(deactivatePost);
            Assert.Equal("{tableId:guid}/deactivate", deactivatePost!.Template);

            var reactivate = Action(nameof(TenantTablesController.Reactivate));
            var reactivatePost = reactivate.GetCustomAttribute<HttpPostAttribute>();
            Assert.NotNull(reactivatePost);
            Assert.Equal("{tableId:guid}/reactivate", reactivatePost!.Template);
        }

        [Fact]
        public void ReadActions_RequireTenantUserPolicy()
        {
            foreach (var name in new[] { nameof(TenantTablesController.List), nameof(TenantTablesController.Get) })
            {
                var action = Action(name);
                var authorize = action.GetCustomAttribute<TenantAuthorizeAttribute>();
                Assert.NotNull(authorize);
                Assert.Equal(TenantPolicies.TenantUser, authorize!.Policy);
                Assert.Equal(TenantAuthenticationSchemes.Bearer, authorize.AuthenticationSchemes);
                Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
            }

            Assert.Null(typeof(TenantTablesController).GetCustomAttribute<AllowAnonymousAttribute>());
        }

        [Fact]
        public void ManagementActions_RequireTablesManagePermission()
        {
            foreach (var name in new[]
            {
                nameof(TenantTablesController.Create),
                nameof(TenantTablesController.BulkAdd),
                nameof(TenantTablesController.BulkDeactivateHighest),
                nameof(TenantTablesController.Put),
                nameof(TenantTablesController.Deactivate),
                nameof(TenantTablesController.Reactivate),
                nameof(TenantTablesController.GetQr),
                nameof(TenantTablesController.GetQrSheet),
                nameof(TenantTablesController.RegenerateQr),
            })
            {
                var action = Action(name);
                var authorize = action.GetCustomAttribute<TenantAuthorizeAttribute>();
                Assert.NotNull(authorize);
                Assert.Equal(CafeHub.Application.Identity.TenantPermissions.TablesManage, authorize!.Policy);
                Assert.Equal(TenantAuthenticationSchemes.Bearer, authorize.AuthenticationSchemes);
                Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
            }
        }

        [Fact]
        public async Task QrActions_SendTheRouteBranchAndReturnCanonicalLinks()
        {
            var branchId = Guid.NewGuid();
            var tableId = Guid.NewGuid();
            var qr = new TableQrData(tableId, 7, "Terrace", "Abcdefghijklmnopqrstuv", 9);
            var sender = new RecordingSender { Response = Result.Success(qr) };
            var controller = CreateController(sender, OwnerUser(), OwnerPermissions());
            var tenantContext = new TenantContextAccessor();
            tenantContext.Set(TenantContext.Create(
                Guid.NewGuid(), "test-reference", true, "canonical-cafe"));
            var links = new TableQrLinks(TableQrLinks.ParseOrigin("https://public.example.test/", false), tenantContext);

            var single = await controller.GetQr("CANONICAL-CAFE", branchId, tableId, links, default);
            Assert.Equal(new GetTableQrQuery(branchId, tableId), Assert.IsType<GetTableQrQuery>(sender.LastRequest));
            var link = Assert.IsType<TableQrLinkResponse>(Assert.IsType<OkObjectResult>(single.Result).Value);
            Assert.Equal("/t/canonical-cafe/q/Abcdefghijklmnopqrstuv", link.Path);
            Assert.Equal("9", link.ConcurrencyToken);

            sender.Response = Result.Success<IReadOnlyList<TableQrData>>([qr]);
            var sheet = await controller.GetQrSheet("CANONICAL-CAFE", branchId, links, default);
            Assert.Equal(new GetTableQrSheetQuery(branchId), Assert.IsType<GetTableQrSheetQuery>(sender.LastRequest));
            Assert.Equal(link, Assert.Single(Assert.IsType<TableQrLinkResponse[]>(Assert.IsType<OkObjectResult>(sheet.Result).Value)));

            sender.Response = Result.Success(qr);
            var rotation = await controller.RegenerateQr("CANONICAL-CAFE", branchId, tableId, new("8"), links, default);
            Assert.Equal(new RegenerateTableQrCommand(branchId, tableId, "8"), Assert.IsType<RegenerateTableQrCommand>(sender.LastRequest));
            Assert.Equal(link, Assert.IsType<OkObjectResult>(rotation.Result).Value);
        }

        [Fact]
        public async Task Create_SendsAddTableCommandAndReturnsCreatedAtTableDetail()
        {
            var response = CreateTableResponse();
            var sender = new RecordingSender { Response = Result.Success(response) };
            var controller = CreateController(sender, OwnerUser(), OwnerPermissions());
            var request = new AddTableRequest("Terrace 3");

            var actionResult = await controller.Create("fixture-cafe", Guid.NewGuid(), request, CancellationToken.None);

            var created = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
            Assert.Equal(nameof(TenantTablesController.Get), created.ActionName);
            var routeValues = created.RouteValues;
            Assert.NotNull(routeValues);
            Assert.Equal("fixture-cafe", routeValues["slug"]);
            Assert.Equal(response.Id, routeValues["tableId"]);
            Assert.Same(response, created.Value);
            var command = Assert.IsType<AddTableCommand>(sender.LastRequest);
            Assert.Equal("Terrace 3", command.Label);
        }

        [Fact]
        public async Task Create_WhenValidationFails_MapsTo400()
        {
            var validationError = ValidationError.Create(new[] { new FieldError("Label", "The table label must be at most 64 characters after trimming.") });
            var sender = new RecordingSender { Response = Result.Failure<TableResponse>(validationError) };
            var controller = CreateController(sender, OwnerUser(), OwnerPermissions());

            var actionResult = await controller.Create(
                "fixture-cafe",
                Guid.NewGuid(),
                new AddTableRequest(new string('a', 65)),
                CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);
            Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
            var problemDetails = Assert.IsType<ValidationProblemDetails>(objectResult.Value);
            Assert.Equal(validationError.Code, problemDetails.Extensions["code"]);
        }

        [Fact]
        public async Task Create_WhenNotFound_MapsTo404()
        {
            var sender = new RecordingSender { Response = Result.Failure<TableResponse>(TablesErrors.NotFound()) };
            var controller = CreateController(sender, OwnerUser(), OwnerPermissions());

            var actionResult = await controller.Create(
                "fixture-cafe",
                Guid.NewGuid(),
                new AddTableRequest(null),
                CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);
            Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
            var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
            Assert.Equal(TablesErrors.NotFoundCode, problem.Extensions["code"]);
        }

        [Fact]
        public async Task Create_WhenConcurrencyConflict_MapsTo409()
        {
            var sender = new RecordingSender { Response = Result.Failure<TableResponse>(TablesErrors.NumberConflict()) };
            var controller = CreateController(sender, OwnerUser(), OwnerPermissions());

            var actionResult = await controller.Create(
                "fixture-cafe",
                Guid.NewGuid(),
                new AddTableRequest(null),
                CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);
            Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);
            var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
            Assert.Equal(TablesErrors.NumberConflictCode, problem.Extensions["code"]);
        }

        [Fact]
        public async Task BulkAdd_SendsAddTablesBulkCommandAndReturnsResult()
        {
            var sender = new RecordingSender { Response = Result.Success(new TableBulkResult(5, 4, 8)) };
            var controller = CreateController(sender, OwnerUser(), OwnerPermissions());

            var actionResult = await controller.BulkAdd("fixture-cafe", Guid.NewGuid(), new AddTablesBulkRequest(5), CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            Assert.Equal(((Result<TableBulkResult>)sender.Response).Value, okResult.Value);
            var command = Assert.IsType<AddTablesBulkCommand>(sender.LastRequest);
            Assert.Equal(5, command.Count);
        }

        [Fact]
        public async Task BulkDeactivateHighest_SendsCommandAndReturnsResult()
        {
            var sender = new RecordingSender { Response = Result.Success(new TableBulkResult(3, 7, 9)) };
            var controller = CreateController(sender, OwnerUser(), OwnerPermissions());

            var actionResult = await controller.BulkDeactivateHighest("fixture-cafe", Guid.NewGuid(), new AddTablesBulkRequest(3), CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            Assert.Equal(((Result<TableBulkResult>)sender.Response).Value, okResult.Value);
            var command = Assert.IsType<DeactivateHighestTablesCommand>(sender.LastRequest);
            Assert.Equal(3, command.Count);
        }

        [Fact]
        public async Task List_SendsListTablesQueryAndReturnsPage()
        {
            var response = CreateTableResponse();
            var page = new OffsetPage<TableResponse>
            {
                Items = new[] { response },
                Page = 1,
                PageSize = 20,
                TotalCount = 1,
                HasNextPage = false,
            };
            var sender = new RecordingSender { Response = Result.Success(page) };
            var controller = CreateController(sender, StaffUser(), StaffWithoutManagePermissions());
            var branchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

            var actionResult = await controller.List(
                "fixture-cafe",
                branchId,
                new ListTablesQuery { BranchId = branchId, Page = 1, PageSize = 20, IncludeTotalCount = true },
                CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var returnedPage = Assert.IsType<OffsetPage<TableResponse>>(okResult.Value);
            Assert.Single(returnedPage.Items);
            Assert.Equal(1, returnedPage.TotalCount);
            var sentQuery = Assert.IsType<ListTablesQuery>(sender.LastRequest);
            Assert.Equal(branchId, sentQuery.BranchId);
            Assert.Equal(1, sentQuery.Page);
            Assert.Equal(20, sentQuery.PageSize);
            Assert.True(sentQuery.IncludeTotalCount);
        }

        [Fact]
        public async Task Get_SendsGetTableQueryAndReturnsTable()
        {
            var response = CreateTableResponse();
            var sender = new RecordingSender { Response = Result.Success(response) };
            var controller = CreateController(sender, StaffUser(), StaffWithManagePermissions());
            var tableId = Guid.NewGuid();

            var actionResult = await controller.Get("fixture-cafe", Guid.NewGuid(), tableId, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var returnedTable = Assert.IsType<TableResponse>(okResult.Value);
            Assert.Equal(response.Id, returnedTable.Id);
            Assert.Equal(response.Number, returnedTable.Number);
            Assert.Equal(response.Status, returnedTable.Status);
            Assert.Equal(response.QrCode, returnedTable.QrCode);
            var query = Assert.IsType<GetTableQuery>(sender.LastRequest);
            Assert.Equal(tableId, query.TableId);
        }

        [Fact]
        public async Task Get_WhenNotFound_MapsTo404()
        {
            var sender = new RecordingSender { Response = Result.Failure<TableResponse>(TablesErrors.NotFound()) };
            var controller = CreateController(sender, StaffUser(), StaffWithoutManagePermissions());

            var actionResult = await controller.Get("fixture-cafe", Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);
            Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
            var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
            Assert.Equal(TablesErrors.NotFoundCode, problem.Extensions["code"]);
        }

        [Fact]
        public async Task Put_SendsUpdateTableCommandAndReturnsTable()
        {
            var response = CreateTableResponse();
            var sender = new RecordingSender { Response = Result.Success(response) };
            var controller = CreateController(sender, OwnerUser(), OwnerPermissions());
            var tableId = Guid.NewGuid();

            var actionResult = await controller.Put(
                "fixture-cafe",
                Guid.NewGuid(),
                tableId,
                new UpdateTableRequest("Counter 7", "12345"),
                CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            Assert.Same(response, okResult.Value);
            var command = Assert.IsType<UpdateTableCommand>(sender.LastRequest);
            Assert.Equal(tableId, command.TableId);
            Assert.Equal("Counter 7", command.Label);
            Assert.Equal("12345", command.ConcurrencyToken);
        }

        [Fact]
        public async Task Deactivate_SendsDeactivateTableCommandAndReturnsTable()
        {
            var response = CreateTableResponse();
            var sender = new RecordingSender { Response = Result.Success(response) };
            var controller = CreateController(sender, OwnerUser(), OwnerPermissions());
            var tableId = Guid.NewGuid();

            var actionResult = await controller.Deactivate(
                "fixture-cafe",
                Guid.NewGuid(),
                tableId,
                new BranchLifecycleRequest("12345"),
                CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            Assert.Same(response, okResult.Value);
            var command = Assert.IsType<DeactivateTableCommand>(sender.LastRequest);
            Assert.Equal(tableId, command.TableId);
            Assert.Equal("12345", command.ConcurrencyToken);
        }

        [Fact]
        public async Task Reactivate_SendsReactivateTableCommandAndReturnsTable()
        {
            var response = CreateTableResponse();
            var sender = new RecordingSender { Response = Result.Success(response) };
            var controller = CreateController(sender, OwnerUser(), OwnerPermissions());
            var tableId = Guid.NewGuid();

            var actionResult = await controller.Reactivate(
                "fixture-cafe",
                Guid.NewGuid(),
                tableId,
                new BranchLifecycleRequest("12345"),
                CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            Assert.Same(response, okResult.Value);
            var command = Assert.IsType<ReactivateTableCommand>(sender.LastRequest);
            Assert.Equal(tableId, command.TableId);
            Assert.Equal("12345", command.ConcurrencyToken);
        }

        [Fact]
        public async Task List_OmitsTheQrCodeForNonManagers()
        {
            var response = CreateTableResponse();
            var page = new OffsetPage<TableResponse> { Items = new[] { response }, Page = 1, PageSize = 20, TotalCount = 1, HasNextPage = false };
            var sender = new RecordingSender { Response = Result.Success(page) };
            var controller = CreateController(sender, StaffUser(), StaffWithoutManagePermissions());

            var actionResult = await controller.List("fixture-cafe", Guid.NewGuid(), new ListTablesQuery { BranchId = Guid.NewGuid() }, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var returnedPage = Assert.IsType<OffsetPage<TableResponse>>(okResult.Value);
            Assert.Null(returnedPage.Items[0].QrCode);
        }

        [Fact]
        public async Task List_IncludesTheQrCodeForManagers()
        {
            var response = CreateTableResponse();
            var page = new OffsetPage<TableResponse> { Items = new[] { response }, Page = 1, PageSize = 20, TotalCount = 1, HasNextPage = false };
            var sender = new RecordingSender { Response = Result.Success(page) };
            var controller = CreateController(sender, OwnerUser(), OwnerPermissions());

            var actionResult = await controller.List("fixture-cafe", Guid.NewGuid(), new ListTablesQuery { BranchId = Guid.NewGuid() }, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var returnedPage = Assert.IsType<OffsetPage<TableResponse>>(okResult.Value);
            Assert.Equal("Abcdefghijklmnopqrstuv", returnedPage.Items[0].QrCode);
        }

        [Fact]
        public async Task Get_OmitsTheQrCodeForNonManagers()
        {
            var response = CreateTableResponse();
            var sender = new RecordingSender { Response = Result.Success(response) };
            var controller = CreateController(sender, StaffUser(), StaffWithoutManagePermissions());

            var actionResult = await controller.Get("fixture-cafe", Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var returnedTable = Assert.IsType<TableResponse>(okResult.Value);
            Assert.Null(returnedTable.QrCode);
        }

        [Fact]
        public async Task Get_IncludesTheQrCodeForManagers()
        {
            var response = CreateTableResponse();
            var sender = new RecordingSender { Response = Result.Success(response) };
            var owner = OwnerUser();
            Assert.False(owner.AccountAccess?.IsStaff ?? true);
            var controller = CreateController(sender, owner, OwnerPermissions());

            var actionResult = await controller.Get("fixture-cafe", Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var returnedTable = Assert.IsType<TableResponse>(okResult.Value);
            Assert.Equal("Abcdefghijklmnopqrstuv", returnedTable.QrCode);
        }

        [Fact]
        public void TheController_RequiresTheSender()
        {
            Assert.Throws<ArgumentNullException>(() => new TenantTablesController(null!, OwnerUser(), OwnerPermissions()));
        }

        private static ICurrentTenantUser OwnerUser() =>
            new FakeCurrentTenantUser
            {
                IsAuthenticated = true,
                UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                AccountAccess = new TenantAccountAccess(false, null, false),
            };

        private static ICurrentTenantUser StaffUser() =>
            new FakeCurrentTenantUser
            {
                IsAuthenticated = true,
                UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                AccountAccess = new TenantAccountAccess(true, null, true) { BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc") },
            };

        private static ITenantPermissionReader OwnerPermissions() =>
            new FakeTenantPermissionReader(Array.Empty<string>());

        private static ITenantPermissionReader StaffWithoutManagePermissions() =>
            new FakeTenantPermissionReader(new[] { "staff.read" });

        private static ITenantPermissionReader StaffWithManagePermissions() =>
            new FakeTenantPermissionReader(new[] { "tables.manage", "staff.read" });

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

        private sealed class FakeCurrentTenantUser : ICurrentTenantUser
        {
            public bool IsAuthenticated { get; set; }
            public Guid? UserId { get; set; }
            public Guid? TenantId { get; set; }
            public IReadOnlyCollection<string> Roles { get; set; } = [];
            public Guid? BranchId { get; set; }
            public TenantAccountAccess? AccountAccess { get; set; }
        }

        private sealed class FakeTenantPermissionReader(IReadOnlyList<string> grants) : ITenantPermissionReader
        {
            public Task<IReadOnlyList<string>> ReadAsync(Guid userId, CancellationToken cancellationToken, Guid? expectedBranchId = null) =>
                Task.FromResult(grants);
        }
    }
}
