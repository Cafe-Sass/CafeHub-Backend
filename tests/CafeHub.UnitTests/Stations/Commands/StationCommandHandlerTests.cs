using CafeHub.Application.Identity;
using CafeHub.Application.Pagination;
using CafeHub.Application.Stations;
using CafeHub.Application.Stations.Commands;
using CafeHub.Application.Stations.Queries;
using CafeHub.Application.Tenancy;
using CafeHub.UnitTests.Branches;

namespace CafeHub.UnitTests.Stations.Commands
{
    public sealed class StationCommandHandlerTests
    {
        private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid ActorId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        private static readonly Guid BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        private readonly FakeStationStore store = new();
        private readonly FakeTask42Access access = new() { TenantId = TenantId, UserId = ActorId };
        private readonly StubTenantContextAccessor tenantContext = new(
            TenantContext.Create(TenantId, "tenant-secret-reference", true, "cafe-one"));

        public StationCommandHandlerTests()
        {
            store.Response = StationResponses.Create(branchId: BranchId);
        }

        [Fact]
        public async Task Create_PassesTheBranchActorAndSortOrderToTheStore()
        {
            var handler = new CreateStationCommandHandler(store, access, tenantContext);

            var result = await handler.Handle(new CreateStationCommand(BranchId, "Bar", 4), default);

            Assert.True(result.IsSuccess);
            var call = Assert.Single(store.Created);
            Assert.Equal(BranchId, call.BranchId);
            Assert.Equal("Bar", call.Name);
            Assert.Equal(4, call.SortOrder);
            Assert.Equal(ActorId, call.ActorUserId);
        }

        [Fact]
        public async Task Create_ForAnotherBranchReturnsUniformNotFoundWithoutCallingTheStore()
        {
            access.AccountAccess = new TenantAccountAccess(true, null, true) { BranchId = Guid.NewGuid() };
            var handler = new CreateStationCommandHandler(store, access, tenantContext);

            var result = await handler.Handle(new CreateStationCommand(BranchId, "Bar", 0), default);

            Assert.Equal(StationsErrors.NotFound(), result.Error);
            Assert.Empty(store.Created);
        }

        [Fact]
        public async Task Create_ForTheStaffMembersOwnBranchIsAllowed()
        {
            access.AccountAccess = new TenantAccountAccess(true, null, true) { BranchId = BranchId };
            var handler = new CreateStationCommandHandler(store, access, tenantContext);

            var result = await handler.Handle(new CreateStationCommand(BranchId, "Bar", 0), default);

            Assert.True(result.IsSuccess);
            Assert.Single(store.Created);
        }

        [Fact]
        public async Task Create_WhenTheTenantContextDoesNotMatchReturnsUnauthorized()
        {
            var handler = new CreateStationCommandHandler(store, access, tenantContext);
            access.TenantId = Guid.NewGuid();

            var result = await handler.Handle(new CreateStationCommand(BranchId, "Bar", 0), default);

            Assert.Equal(AuthErrors.CurrentTenantUserUnavailableCode, result.Error!.Code);
            Assert.Empty(store.Created);
        }

        [Fact]
        public async Task UpdateAndLifecycleCommands_ForwardTheStaffBranchScope()
        {
            access.AccountAccess = new TenantAccountAccess(true, null, true) { BranchId = BranchId };
            var stationId = Guid.NewGuid();

            await new UpdateStationCommandHandler(store, access, tenantContext)
                .Handle(new UpdateStationCommand(stationId, "Bar", 1, "7"), default);
            await new DeactivateStationCommandHandler(store, access, tenantContext)
                .Handle(new DeactivateStationCommand(stationId, "7"), default);
            await new ReactivateStationCommandHandler(store, access, tenantContext)
                .Handle(new ReactivateStationCommand(stationId, "7"), default);

            Assert.Equal(BranchId, Assert.Single(store.Updated).ScopeBranchId);
            Assert.Equal(BranchId, Assert.Single(store.Deactivated).ScopeBranchId);
            Assert.Equal(BranchId, Assert.Single(store.Reactivated).ScopeBranchId);
        }

        [Fact]
        public async Task Get_UsesTheStaffBranchScopeAndReturnsNotFoundForAMissingStation()
        {
            access.AccountAccess = new TenantAccountAccess(true, null, true) { BranchId = BranchId };
            var stationId = Guid.NewGuid();
            var handler = new GetStationQueryHandler(store, access);

            var missing = await handler.Handle(new GetStationQuery(stationId), default);
            store.GetByIdResponse = StationResponses.Create(stationId, BranchId);
            var found = await handler.Handle(new GetStationQuery(stationId), default);

            Assert.Equal(StationsErrors.NotFound(), missing.Error);
            Assert.True(found.IsSuccess);
            Assert.All(store.GetByIdRequests, request => Assert.Equal(BranchId, request.ScopeBranchId));
        }

        [Fact]
        public async Task List_PagesWithTheSharedOffsetContractAndRejectsAnotherBranch()
        {
            store.ListResponses.Add(StationResponses.Create(branchId: BranchId));
            store.ListResponses.Add(StationResponses.Create(branchId: BranchId));
            var handler = new ListStationsQueryHandler(store, access, new PaginationOptions());

            var page = await handler.Handle(
                new ListStationsQuery { BranchId = BranchId, Page = 1, PageSize = 1 },
                default);

            Assert.True(page.IsSuccess);
            Assert.Single(page.Value!.Items);
            Assert.True(page.Value.HasNextPage);
            Assert.Null(page.Value.TotalCount);
            Assert.Equal(BranchId, Assert.Single(store.ListRequests).BranchId);

            access.AccountAccess = new TenantAccountAccess(true, null, true) { BranchId = Guid.NewGuid() };
            var other = await new ListStationsQueryHandler(store, access, new PaginationOptions())
                .Handle(new ListStationsQuery { BranchId = BranchId }, default);

            Assert.Equal(StationsErrors.NotFound(), other.Error);
        }
    }
}
