using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;

namespace CafeHub.UnitTests.Stations.Persistence
{
    public sealed class StationAuditChangeSetTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 15, 0, 0, 0, TimeSpan.Zero);
        private static readonly Guid BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        [Fact]
        public void StationCreate_CapturesTheApprovedNewValuesOnly()
        {
            var station = CreateStation(" Bar ", 3);

            var changes = TenantAuditChangeSets.StationCreate(station);

            Assert.True(changes.IsCreate);
            Assert.Equal(
                new[] { "Name", "SortOrder", "Status", "BranchId" },
                changes.Fields.Select(field => field.Name).ToArray());
            Assert.All(changes.Fields, field => Assert.Null(field.Before));
            Assert.Equal("Bar", changes.Fields[0].After);
            Assert.Equal("3", changes.Fields[1].After);
            Assert.Equal("Active", changes.Fields[2].After);
            Assert.Equal(BranchId.ToString(), changes.Fields[3].After);
        }

        [Fact]
        public void StationCreate_SerializesToABeforeNullSnapshot()
        {
            var result = TenantAuditSnapshotSerializer.Create(
                "stations",
                TenantAuditChangeSets.StationCreate(CreateStation("Bar", 3)));

            Assert.True(result.IsSuccess);
            Assert.Null(result.Value.BeforeState);
            Assert.Equal(
                "{\"BranchId\":\"cccccccc-cccc-cccc-cccc-cccccccccccc\",\"Name\":\"Bar\",\"SortOrder\":\"3\",\"Status\":\"Active\"}",
                result.Value.AfterState);
        }

        [Fact]
        public void StationUpdate_CapturesTheOldAndNewNameAndSortOrder()
        {
            var station = CreateStation("Bar", 3);

            var changes = TenantAuditChangeSets.StationUpdate(station, "Kitchen", 4);

            Assert.False(changes.IsCreate);
            Assert.Equal(new[] { "Name", "SortOrder" }, changes.Fields.Select(field => field.Name).ToArray());
            Assert.Equal("Bar", changes.Fields[0].Before);
            Assert.Equal("Kitchen", changes.Fields[0].After);
            Assert.Equal("3", changes.Fields[1].Before);
            Assert.Equal("4", changes.Fields[1].After);
        }

        [Fact]
        public void StationUpdate_WithNoEffectiveChange_ProducesNoSnapshot()
        {
            var station = CreateStation("Bar", 3);

            var result = TenantAuditSnapshotSerializer.Create(
                "stations",
                TenantAuditChangeSets.StationUpdate(station, "Bar", 3));

            Assert.True(result.IsSuccess);
            Assert.Null(result.Value.BeforeState);
            Assert.Null(result.Value.AfterState);
        }

        [Fact]
        public void StationLifecycleStatus_CapturesBothDirections()
        {
            var active = CreateStation("Bar", 3);
            var deactivated = TenantAuditSnapshotSerializer.Create(
                "stations",
                TenantAuditChangeSets.StationLifecycleStatus(active, StationStatus.Inactive)).Value;
            Assert.Contains("\"Status\":\"Active\"", deactivated.BeforeState);
            Assert.Contains("\"Status\":\"Inactive\"", deactivated.AfterState);

            active.Deactivate(Now, null);
            var reactivated = TenantAuditSnapshotSerializer.Create(
                "stations",
                TenantAuditChangeSets.StationLifecycleStatus(active, StationStatus.Active)).Value;
            Assert.Contains("\"Status\":\"Inactive\"", reactivated.BeforeState);
            Assert.Contains("\"Status\":\"Active\"", reactivated.AfterState);
        }

        private static Station CreateStation(string name, int sortOrder) =>
            Station.Create(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"), BranchId, name, sortOrder, Now);
    }
}
