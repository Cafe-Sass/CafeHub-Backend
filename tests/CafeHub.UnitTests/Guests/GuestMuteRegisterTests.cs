using System.Reflection;
using System.Text.Json;
using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Guests;
using CafeHub.Application.Guests.Commands;
using CafeHub.Application.Guests.Contracts;
using CafeHub.Application.Guests.DTOs;
using CafeHub.Application.Guests.Queries;
using CafeHub.Application.Identity;
using CafeHub.Application.Pagination;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Controllers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace CafeHub.UnitTests.Guests
{
    public sealed class GuestMuteRegisterTests
    {
        private static readonly Guid BranchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid OtherBranchId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly Guid OrderId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        private static readonly Guid MuteId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-23T10:00:00Z");
        private static readonly DateOnly From = new(2026, 11, 20);
        private static readonly DateOnly To = new(2026, 11, 23);
        private const string DeviceId = "device-0123456789abcdef";

        [Fact]
        public void Controller_IsAnApiControllerOnTheGuestMutesRoute()
        {
            Assert.True(typeof(ControllerBase).IsAssignableFrom(typeof(TenantGuestMutesController)));
            Assert.NotNull(typeof(TenantGuestMutesController).GetCustomAttribute<ApiControllerAttribute>());
            Assert.Equal(
                "t/{slug}/api/v1/guest-mutes",
                typeof(TenantGuestMutesController).GetCustomAttribute<RouteAttribute>()!.Template);
        }

        [Fact]
        public void Controller_TheMuteActionRequiresTheGuestsMutePolicy()
        {
            var action = Action(nameof(TenantGuestMutesController.Mute));

            Assert.NotNull(action.GetCustomAttribute<HttpPostAttribute>());
            Assert.Null(action.GetCustomAttribute<HttpPostAttribute>()!.Template);
            var authorize = Assert.IsType<TenantAuthorizeAttribute>(action.GetCustomAttribute<TenantAuthorizeAttribute>());
            Assert.Equal(TenantPermissions.GuestsMute, authorize.Policy);
        }

        [Fact]
        public void Controller_TheLiftActionRequiresTheGuestsMutePolicy()
        {
            var action = Action(nameof(TenantGuestMutesController.Lift));

            Assert.Equal("{muteId:guid}/lift", action.GetCustomAttribute<HttpPostAttribute>()!.Template);
            var authorize = Assert.IsType<TenantAuthorizeAttribute>(action.GetCustomAttribute<TenantAuthorizeAttribute>());
            Assert.Equal(TenantPermissions.GuestsMute, authorize.Policy);
        }

        [Fact]
        public void Controller_TheListActionRequiresTheGuestsMuteAuditPolicy()
        {
            var action = Action(nameof(TenantGuestMutesController.List));

            Assert.NotNull(action.GetCustomAttribute<HttpGetAttribute>());
            var authorize = Assert.IsType<TenantAuthorizeAttribute>(action.GetCustomAttribute<TenantAuthorizeAttribute>());
            Assert.Equal(TenantPermissions.GuestsMuteAudit, authorize.Policy);
        }

        [Fact]
        public void Controller_TheSummaryActionRequiresTheGuestsMuteAuditPolicy()
        {
            var action = Action(nameof(TenantGuestMutesController.Summary));

            Assert.Equal("summary", action.GetCustomAttribute<HttpGetAttribute>()!.Template);
            var authorize = Assert.IsType<TenantAuthorizeAttribute>(action.GetCustomAttribute<TenantAuthorizeAttribute>());
            Assert.Equal(TenantPermissions.GuestsMuteAudit, authorize.Policy);
        }

        [Fact]
        public void Controller_EveryHttpActionDeclaresATenantPolicyAndNoActionIsAnonymous()
        {
            var actions = typeof(TenantGuestMutesController)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => method.GetCustomAttributes().Any(attribute => attribute is HttpMethodAttribute))
                .ToArray();

            Assert.Equal(4, actions.Length);
            foreach (var action in actions)
            {
                var authorize = Assert.IsType<TenantAuthorizeAttribute>(
                    action.GetCustomAttribute<TenantAuthorizeAttribute>());
                Assert.True(
                    authorize.Policy is TenantPermissions.GuestsMute or TenantPermissions.GuestsMuteAudit,
                    $"{action.Name} declares an unexpected policy.");
                Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
            }
        }

        [Fact]
        public async Task Controller_MuteReturnsCreatedWithTheMute()
        {
            var sender = new RecordingSender { Response = Result.Success(MuteResult()) };
            var controller = CreateController(sender);

            var actionResult = await controller.Mute(
                "fixture-cafe",
                new MuteGuestDeviceRequest(OrderId, "Spam", 6),
                CancellationToken.None);

            var created = Assert.IsType<CreatedResult>(actionResult.Result);
            Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
            Assert.Equal($"/t/fixture-cafe/api/v1/guest-mutes/{MuteId}", created.Location);
            var body = Assert.IsType<GuestMuteResponse>(created.Value);
            Assert.Equal(12, body.OrderNumber);
            Assert.Equal("Sara", body.MutedByName);
            var command = Assert.IsType<MuteGuestDeviceCommand>(sender.LastRequest);
            Assert.Equal(OrderId, command.OrderId);
            Assert.Equal("Spam", command.Reason);
            Assert.Equal(6, command.ExpiryHours);
        }

        [Fact]
        public async Task Controller_MuteMapsAValidationFailureTo400()
        {
            var sender = new RecordingSender
            {
                Response = Result.Failure<GuestMuteResult>(GuestMuteErrors.DeviceRequired()),
            };
            var controller = CreateController(sender);

            var actionResult = await controller.Mute(
                "fixture-cafe",
                new MuteGuestDeviceRequest(OrderId, null, null),
                CancellationToken.None);

            var problemResult = Assert.IsType<ObjectResult>(actionResult.Result);
            Assert.Equal(StatusCodes.Status400BadRequest, problemResult.StatusCode);
            var problem = Assert.IsType<ProblemDetails>(problemResult.Value);
            Assert.Equal(GuestMuteErrors.DeviceRequiredCode, problem.Extensions["code"]);
            Assert.Equal(string.Empty, Assert.IsType<MuteGuestDeviceCommand>(sender.LastRequest).Reason);
        }

        [Fact]
        public async Task Controller_MuteMapsAForeignOrderTo404()
        {
            var sender = new RecordingSender
            {
                Response = Result.Failure<GuestMuteResult>(GuestMuteErrors.NotFound()),
            };
            var controller = CreateController(sender);

            var actionResult = await controller.Mute(
                "fixture-cafe",
                new MuteGuestDeviceRequest(OrderId, "Spam", null),
                CancellationToken.None);

            var problemResult = Assert.IsType<ObjectResult>(actionResult.Result);
            Assert.Equal(StatusCodes.Status404NotFound, problemResult.StatusCode);
            var problem = Assert.IsType<ProblemDetails>(problemResult.Value);
            Assert.Equal(GuestMuteErrors.NotFoundCode, problem.Extensions["code"]);
        }

        [Fact]
        public async Task Controller_LiftReturnsOk()
        {
            var sender = new RecordingSender { Response = Result.Success(MuteResult() with { LiftedAt = Now }) };
            var controller = CreateController(sender);

            var actionResult = await controller.Lift(MuteId, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var body = Assert.IsType<GuestMuteResponse>(okResult.Value);
            Assert.Equal(Now, body.LiftedAt);
            Assert.Equal(MuteId, Assert.IsType<UnmuteGuestDeviceCommand>(sender.LastRequest).MuteId);
        }

        [Fact]
        public async Task Controller_LiftMapsAForeignMuteTo404()
        {
            var sender = new RecordingSender
            {
                Response = Result.Failure<GuestMuteResult>(GuestMuteErrors.NotFound()),
            };
            var controller = CreateController(sender);

            var actionResult = await controller.Lift(MuteId, CancellationToken.None);

            var problemResult = Assert.IsType<ObjectResult>(actionResult.Result);
            Assert.Equal(StatusCodes.Status404NotFound, problemResult.StatusCode);
        }

        [Fact]
        public async Task Controller_ListReturnsThePage()
        {
            var page = new OffsetPage<GuestMuteListItem>
            {
                Items = [Item(Now)],
                Page = 2,
                PageSize = 5,
                TotalCount = 6,
                HasNextPage = true,
            };
            var sender = new RecordingSender { Response = Result.Success(page) };
            var controller = CreateController(sender);

            var actionResult = await controller.List(
                new GetGuestMutesQuery { Page = 2, PageSize = 5, IncludeTotalCount = true },
                CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var returned = Assert.IsType<OffsetPage<GuestMuteListItem>>(okResult.Value);
            Assert.Equal(6, returned.TotalCount);
            Assert.Equal("Sara", Assert.Single(returned.Items).MutedByName);
        }

        [Fact]
        public async Task Controller_SummaryReturnsTheCounts()
        {
            IReadOnlyList<GuestMuteSummaryEntry> entries =
            [
                new GuestMuteSummaryEntry(UserId, "Sara", 4, 1),
            ];
            var sender = new RecordingSender { Response = Result.Success(entries) };
            var controller = CreateController(sender);

            var actionResult = await controller.Summary(
                new GetGuestMuteSummaryQuery { From = From, To = To },
                CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var returned = Assert.IsAssignableFrom<IReadOnlyList<GuestMuteSummaryEntry>>(okResult.Value);
            Assert.Equal(4, Assert.Single(returned).MuteCount);
        }

        [Fact]
        public async Task List_ForwardsTheFiltersScopeAndPagingToTheStore()
        {
            var store = new FakeGuestMuteStore { Entries = [Entry()] };
            var handler = CreateListHandler(store, StaffUser(BranchId));

            var result = await handler.Handle(
                new GetGuestMutesQuery
                {
                    Page = 2,
                    PageSize = 5,
                    IncludeTotalCount = true,
                    Active = true,
                    MutedByUserId = UserId,
                },
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            var request = Assert.Single(store.ListRequests);
            Assert.Equal(BranchId, request.BranchId);
            Assert.Equal(true, request.IsActive);
            Assert.Equal(UserId, request.MutedByUserId);
            Assert.Equal(2, request.Page);
            Assert.Equal(5, request.PageSize);
            Assert.True(request.IncludeTotalCount);
            Assert.Equal(Now, request.Now);
            Assert.Equal(2, result.Value!.Page);
            Assert.False(result.Value.HasNextPage);
            Assert.Single(result.Value.Items);
        }

        [Fact]
        public async Task List_UsesTheConfiguredDefaultPageSize()
        {
            var store = new FakeGuestMuteStore { Entries = [] };
            var handler = CreateListHandler(store, StaffUser(BranchId));

            await handler.Handle(new GetGuestMutesQuery(), CancellationToken.None);

            Assert.Equal(20, Assert.Single(store.ListRequests).PageSize);
        }

        [Fact]
        public async Task List_AForeignBranchReturnsTheUniformNotFound()
        {
            var store = new FakeGuestMuteStore { Entries = [] };
            var handler = CreateListHandler(store, StaffUser(BranchId));

            var result = await handler.Handle(
                new GetGuestMutesQuery { BranchId = OtherBranchId },
                CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(GuestMuteErrors.NotFoundCode, result.Error!.Code);
            Assert.Empty(store.ListRequests);
        }

        [Fact]
        public async Task List_TheOwnerMustPassABranch()
        {
            var store = new FakeGuestMuteStore { Entries = [] };
            var handler = CreateListHandler(store, OwnerUser());

            var result = await handler.Handle(new GetGuestMutesQuery(), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(GuestMuteErrors.BranchRequiredCode, result.Error!.Code);
            Assert.Empty(store.ListRequests);
        }

        [Fact]
        public async Task List_TheOwnerMayReadAnyBranch()
        {
            var store = new FakeGuestMuteStore { Entries = [] };
            var handler = CreateListHandler(store, OwnerUser());

            var result = await handler.Handle(
                new GetGuestMutesQuery { BranchId = OtherBranchId },
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(OtherBranchId, Assert.Single(store.ListRequests).BranchId);
        }

        [Fact]
        public async Task List_TheDeviceReferenceIsMaskedAndTheRawValueIsNotReturned()
        {
            var store = new FakeGuestMuteStore { Entries = [Entry()] };
            var handler = CreateListHandler(store, OwnerUser());

            var result = await handler.Handle(
                new GetGuestMutesQuery { BranchId = BranchId },
                CancellationToken.None);

            var item = Assert.Single(result.Value!.Items);
            Assert.Equal("devi\u2026cdef", item.DeviceReference);
            Assert.DoesNotContain(DeviceId, JsonSerializer.Serialize(item), StringComparison.Ordinal);
        }

        [Fact]
        public async Task List_AnExpiredMuteIsReportedAsInactive()
        {
            var store = new FakeGuestMuteStore
            {
                Entries = [Entry() with { ExpiresAt = Now.AddMinutes(-1) }],
            };
            var handler = CreateListHandler(store, StaffUser(BranchId));

            var result = await handler.Handle(new GetGuestMutesQuery(), CancellationToken.None);

            Assert.False(Assert.Single(result.Value!.Items).IsActive);
        }

        [Fact]
        public void List_TheValidatorRefusesAPageSizeAboveTheMaximum()
        {
            var validator = new GetGuestMutesQueryValidator(PaginationOptions());

            var validation = validator.Validate(new GetGuestMutesQuery { PageSize = 101 });

            Assert.False(validation.IsValid);
        }

        [Fact]
        public async Task Summary_ForwardsTheUtcDateRangeAndBranchToTheStore()
        {
            var store = new FakeGuestMuteStore
            {
                Summary =
                [
                    new GuestMuteSummaryEntry(UserId, "Sara", 4, 1),
                    new GuestMuteSummaryEntry(Guid.CreateVersion7(), "Omar", 2, 0),
                ],
            };
            var handler = CreateSummaryHandler(store, StaffUser(BranchId));

            var result = await handler.Handle(
                new GetGuestMuteSummaryQuery { From = From, To = To },
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            var request = Assert.Single(store.SummaryRequests);
            Assert.Equal(BranchId, request.BranchId);
            Assert.Equal(new DateTimeOffset(2026, 11, 20, 0, 0, 0, TimeSpan.Zero), request.From);
            Assert.Equal(new DateTimeOffset(2026, 11, 24, 0, 0, 0, TimeSpan.Zero), request.ToExclusive);
            Assert.Equal(4, result.Value![0].MuteCount);
            Assert.Equal(1, result.Value[0].LiftedEarlyCount);
            Assert.Equal(2, result.Value.Count);
        }

        [Fact]
        public async Task Summary_AStaffMemberIsFixedToTheirBranch()
        {
            var store = new FakeGuestMuteStore { Summary = [] };
            var handler = CreateSummaryHandler(store, StaffUser(BranchId));

            var result = await handler.Handle(
                new GetGuestMuteSummaryQuery { BranchId = OtherBranchId, From = From, To = To },
                CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(GuestMuteErrors.NotFoundCode, result.Error!.Code);
            Assert.Empty(store.SummaryRequests);
        }

        [Fact]
        public async Task Summary_TheOwnerMustPassABranch()
        {
            var store = new FakeGuestMuteStore { Summary = [] };
            var handler = CreateSummaryHandler(store, OwnerUser());

            var result = await handler.Handle(
                new GetGuestMuteSummaryQuery { From = From, To = To },
                CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(GuestMuteErrors.BranchRequiredCode, result.Error!.Code);
        }

        [Fact]
        public void Summary_TheValidatorRefusesAReversedRange()
        {
            var validator = new GetGuestMuteSummaryQueryValidator();

            var validation = validator.Validate(new GetGuestMuteSummaryQuery { From = To, To = From });

            Assert.False(validation.IsValid);
            Assert.Contains(validation.Errors, error => error.ErrorMessage == GuestMuteErrors.DateRangeInvalid().Message);
        }

        private static MethodInfo Action(string name) =>
            typeof(TenantGuestMutesController).GetMethod(name, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"The TenantGuestMutesController.{name} action was not found.");

        private static GuestMuteResult MuteResult() => new(
            MuteId,
            OrderId,
            12,
            "Spam",
            Now.AddHours(-1),
            Now.AddHours(23),
            null,
            UserId)
        {
            MutedByName = "Sara",
        };

        private static GuestMuteListItem Item(DateTimeOffset now) => GuestMuteListItem.From(Entry(), now);

        private static GuestMuteEntry Entry() => new(
            MuteId,
            BranchId,
            OrderId,
            12,
            UserId,
            "Spam",
            Now.AddHours(-1),
            Now.AddHours(23),
            null,
            null)
        {
            DeviceId = DeviceId,
            MutedByName = "Sara",
        };

        private static TenantGuestMutesController CreateController(ISender sender) =>
            new(sender, new TestCorrelationIdAccessor())
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-guest-mutes" },
                },
            };

        private static GetGuestMutesQueryHandler CreateListHandler(
            FakeGuestMuteStore store,
            FakeTenantUser user) =>
            new(store, user, PaginationOptions(), new FixedClock());

        private static GetGuestMuteSummaryQueryHandler CreateSummaryHandler(
            FakeGuestMuteStore store,
            FakeTenantUser user) =>
            new(store, user);

        private static PaginationOptions PaginationOptions() => new()
        {
            DefaultPageSize = 20,
            MaxPageSize = 100,
        };

        private static FakeTenantUser StaffUser(Guid branchId) => new()
        {
            UserId = UserId,
            BranchId = branchId,
            AccountAccess = new TenantAccountAccess(true, null, true) { BranchId = branchId },
        };

        private static FakeTenantUser OwnerUser() => new()
        {
            UserId = Guid.CreateVersion7(),
            AccountAccess = new TenantAccountAccess(false, null, false),
        };

        private sealed class FixedClock : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => Now;
        }

        private sealed class FakeTenantUser : ICurrentTenantUser
        {
            public bool IsAuthenticated => true;

            public Guid? UserId { get; init; }

            public Guid? TenantId { get; init; } = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

            public IReadOnlyCollection<string> Roles => [];

            public Guid? BranchId { get; init; }

            public TenantAccountAccess? AccountAccess { get; init; }
        }

        private sealed class FakeGuestMuteStore : IGuestMuteStore
        {
            public IReadOnlyList<GuestMuteEntry> Entries { get; init; } = [];

            public IReadOnlyList<GuestMuteSummaryEntry> Summary { get; init; } = [];

            public List<GuestMuteListRequest> ListRequests { get; } = [];

            public List<GuestMuteSummaryRequest> SummaryRequests { get; } = [];

            public Task<GuestMuteTarget?> FindOrderAsync(
                Guid? branchId, Guid orderId, CancellationToken cancellationToken) =>
                Task.FromResult<GuestMuteTarget?>(null);

            public Task<GuestMuteEntry?> FindActiveAsync(
                Guid branchId, string deviceId, DateTimeOffset now, CancellationToken cancellationToken) =>
                Task.FromResult<GuestMuteEntry?>(null);

            public Task<GuestMuteEntry?> FindAsync(
                Guid? branchId, Guid muteId, CancellationToken cancellationToken) =>
                Task.FromResult<GuestMuteEntry?>(null);

            public Task<GuestMuteListPage> ListAsync(
                GuestMuteListRequest request, CancellationToken cancellationToken)
            {
                ListRequests.Add(request);
                return Task.FromResult(new GuestMuteListPage(Entries, Entries.Count));
            }

            public Task<IReadOnlyList<GuestMuteSummaryEntry>> ListSummaryAsync(
                GuestMuteSummaryRequest request, CancellationToken cancellationToken)
            {
                SummaryRequests.Add(request);
                return Task.FromResult(Summary);
            }

            public Task<string?> FindUserNameAsync(Guid userId, CancellationToken cancellationToken) =>
                Task.FromResult<string?>("Sara");

            public Task<Result> CreateAsync(
                CafeHub.Domain.Tenants.GuestMute mute, CancellationToken cancellationToken) =>
                Task.FromResult(Result.Success());

            public Task<Result> LiftAsync(
                Guid muteId, Guid liftedByUserId, DateTimeOffset now, CancellationToken cancellationToken) =>
                Task.FromResult(Result.Success());
        }

        private sealed class RecordingSender : ISender
        {
            public object? Response { get; init; }

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
            public string? CorrelationId => null;
        }
    }
}
