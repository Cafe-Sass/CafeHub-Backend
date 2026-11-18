using System.Data;
using CafeHub.Application.Menu;
using CafeHub.Application.Menu.DTOs;
using CafeHub.Infrastructure.Menu;
using CafeHub.UnitTests.Branches;

namespace CafeHub.UnitTests.Menu.Persistence
{
    public sealed class ProductRoutingTests
    {
        [Theory]
        [InlineData(false, true, true, ProductRoutingState.BranchInactive)]
        [InlineData(true, false, false, ProductRoutingState.Unassigned)]
        [InlineData(true, true, false, ProductRoutingState.StationInactive)]
        [InlineData(true, true, true, ProductRoutingState.Assigned)]
        public void AvailabilityDoesNotChangeRoutingAndOnlyEligibleAvailableProductsCanOrder(
            bool branchActive, bool assigned, bool stationActive, ProductRoutingState expected)
        {
            var state = ProductStationAssignmentStore.Classify(
                branchActive, assigned ? Guid.NewGuid() : null, stationActive);

            Assert.Equal(expected, state);
            Assert.False(new ProductRouting(state, null, ProductAvailable: false).CanOrder);
            Assert.Equal(expected is ProductRoutingState.Assigned or ProductRoutingState.Unassigned,
                new ProductRouting(state, null, ProductAvailable: true).CanOrder);
        }

        [Theory]
        [InlineData(false, false, null)]
        [InlineData(true, false, ProductRoutingState.Unassigned)]
        [InlineData(true, true, ProductRoutingState.StationInactive)]
        public async Task RoutingQueryUsesCurrentBranchAssignmentAndTenantReadiness(
            bool ready, bool assigned, ProductRoutingState? expected)
        {
            var productId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var stationId = Guid.NewGuid();
            var stub = new TenantCommandStub((command, _) =>
            {
                Assert.Contains("product_routing_readiness", command.CommandText);
                Assert.Contains("product_station_assignments", command.CommandText);
                Assert.Contains("ended_at IS NULL", command.CommandText);
                Assert.Contains("menu_categories", command.CommandText);
                Assert.Contains("is_default", command.CommandText);
                Assert.Contains("xmin", command.CommandText);
                Assert.Contains(command.Parameters.Cast<System.Data.Common.DbParameter>(), parameter => Equals(parameter.Value, branchId));

                var rows = new DataTable();
                rows.Columns.Add("ready", typeof(bool));
                rows.Columns.Add("branch_active", typeof(bool));
                rows.Columns.Add("orderable", typeof(bool));
                rows.Columns.Add("assignment_id", typeof(Guid));
                rows.Columns.Add("station_id", typeof(Guid));
                rows.Columns.Add("station_active", typeof(bool));
                rows.Columns.Add("version", typeof(uint));
                rows.Rows.Add(ready, true, true, assigned ? Guid.NewGuid() : DBNull.Value,
                    assigned ? stationId : DBNull.Value, false, DBNull.Value);
                return rows;
            }, _ => { });
            await using var context = stub.CreateContext();
            var store = new ProductStationAssignmentStore(
                context, new RecordingTenantAuditWriter(), TimeProvider.System);

            var routing = await store.ResolveRoutingAsync(productId, branchId, default);

            if (!ready)
            {
                Assert.Equal(ProductErrors.RoutingNotReady(), routing.Error);
                return;
            }

            Assert.True(routing.IsSuccess);
            Assert.Equal(expected, routing.Value.State);
            Assert.Null(routing.Value.StationId);
        }

        [Fact]
        public void AssignmentTokenIsBoundToBranchAndProductAndSeparatesUnusedFromCleared()
        {
            var productId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var empty = ProductStationAssignmentStore.Token(productId, branchId, null);

            Assert.NotEqual(empty, ProductStationAssignmentStore.Token(productId, branchId, 5));
            Assert.NotEqual(empty, ProductStationAssignmentStore.Token(productId, Guid.NewGuid(), null));
            Assert.NotEqual(empty, ProductStationAssignmentStore.Token(Guid.NewGuid(), branchId, null));
        }
    }
}
