using CafeHub.Application.Identity;
using CafeHub.Application.Stations;
using CafeHub.Application.Stations.Commands;
using CafeHub.Application.Tenancy;
using CafeHub.UnitTests.Branches;

namespace CafeHub.UnitTests.Stations.Commands
{
    public sealed class SetStaffStationsCommandHandlerTests
    {
        private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid ActorId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        private static readonly Guid BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        private static readonly Guid StaffId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        private static readonly Guid StationId1 = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        private static readonly Guid StationId2 = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");

        private readonly FakeStationStore store = new();
        private readonly FakeTask42Access access = new() { TenantId = TenantId, UserId = ActorId };
        private readonly StubTenantContextAccessor tenantContext = new(
            TenantContext.Create(TenantId, "tenant-secret-reference", true, "cafe-one"));

        [Fact]
        public async Task SetStaffStations_ReplacesTheStationSet()
        {
            access.AccountAccess = new TenantAccountAccess(true, null, true) { BranchId = BranchId };
            var handler = new SetStaffStationsCommandHandler(store, access, tenantContext);

            var result = await handler.Handle(
                new SetStaffStationsCommand(StaffId, new[] { StationId1, StationId2 }),
                default);

            Assert.True(result.IsSuccess);
            var call = Assert.Single(store.StaffStationAssignments);
            Assert.Equal(StaffId, call.StaffId);
            Assert.Equal(2, call.StationIds.Count);
            Assert.Equal(StationId1, call.StationIds[0]);
            Assert.Equal(StationId2, call.StationIds[1]);
            Assert.Equal(ActorId, call.ActorUserId);
        }

        [Fact]
        public async Task SetStaffStations_EmptyListClearsAllAssignments()
        {
            access.AccountAccess = new TenantAccountAccess(true, null, true) { BranchId = BranchId };
            var handler = new SetStaffStationsCommandHandler(store, access, tenantContext);

            var result = await handler.Handle(
                new SetStaffStationsCommand(StaffId, Array.Empty<Guid>()),
                default);

            Assert.True(result.IsSuccess);
            var call = Assert.Single(store.StaffStationAssignments);
            Assert.Empty(call.StationIds);
        }

        [Fact]
        public async Task SetStaffStations_ForwardsTheStaffBranchScopeToTheStore()
        {
            access.AccountAccess = new TenantAccountAccess(true, null, true) { BranchId = BranchId };
            var handler = new SetStaffStationsCommandHandler(store, access, tenantContext);

            var result = await handler.Handle(
                new SetStaffStationsCommand(StaffId, new[] { StationId1 }),
                default);

            Assert.True(result.IsSuccess);
            var call = Assert.Single(store.StaffStationAssignments);
            Assert.Equal(BranchId, call.ScopeBranchId);
        }

        [Fact]
        public async Task SetStaffStations_ForTheStaffMembersOwnBranchIsAllowed()
        {
            access.AccountAccess = new TenantAccountAccess(true, null, true) { BranchId = BranchId };
            var handler = new SetStaffStationsCommandHandler(store, access, tenantContext);

            var result = await handler.Handle(
                new SetStaffStationsCommand(StaffId, new[] { StationId1 }),
                default);

            Assert.True(result.IsSuccess);
            Assert.Single(store.StaffStationAssignments);
        }

        [Fact]
        public async Task SetStaffStations_WhenTheTenantContextDoesNotMatchReturnsUnauthorized()
        {
            var handler = new SetStaffStationsCommandHandler(store, access, tenantContext);
            access.TenantId = Guid.NewGuid();

            var result = await handler.Handle(
                new SetStaffStationsCommand(StaffId, new[] { StationId1 }),
                default);

            Assert.Equal(AuthErrors.CurrentTenantUserUnavailableCode, result.Error!.Code);
            Assert.Empty(store.StaffStationAssignments);
        }

        [Fact]
        public async Task SetStaffStations_OwnerCanSetStationsForAnyBranch()
        {
            access.AccountAccess = new(false, null, false);
            var handler = new SetStaffStationsCommandHandler(store, access, tenantContext);

            var result = await handler.Handle(
                new SetStaffStationsCommand(StaffId, new[] { StationId1 }),
                default);

            Assert.True(result.IsSuccess);
            var call = Assert.Single(store.StaffStationAssignments);
            Assert.Null(call.ScopeBranchId);
        }
    }
}
