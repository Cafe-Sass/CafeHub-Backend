using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;

namespace CafeHub.UnitTests.Tables.Persistence
{
    public sealed class TableAuditChangeSetTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 15, 0, 0, 0, TimeSpan.Zero);
        private static readonly Guid BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        private static readonly Guid TableId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

        [Fact]
        public void TableCreate_CapturesTheApprovedNewValuesOnly()
        {
            var table = CreateTable("  Terrace 3  ", 5);

            var changes = TenantAuditChangeSets.TableCreate(table);

            Assert.True(changes.IsCreate);
            Assert.Equal(
                new[] { "Number", "Label", "Status", "BranchId" },
                changes.Fields.Select(field => field.Name).ToArray());
            Assert.All(changes.Fields, field => Assert.Null(field.Before));
            Assert.Equal("5", changes.Fields[0].After);
            Assert.Equal("Terrace 3", changes.Fields[1].After);
            Assert.Equal("Active", changes.Fields[2].After);
            Assert.Equal(BranchId.ToString(), changes.Fields[3].After);
        }

        [Fact]
        public void TableCreate_SerializesToABeforeNullSnapshot()
        {
            var result = TenantAuditSnapshotSerializer.Create(
                "branch_tables",
                TenantAuditChangeSets.TableCreate(CreateTable("Bar", 3)));

            Assert.True(result.IsSuccess);
            Assert.Null(result.Value.BeforeState);
            Assert.Equal(
                "{\"BranchId\":\"cccccccc-cccc-cccc-cccc-cccccccccccc\",\"Label\":\"Bar\",\"Number\":\"3\",\"Status\":\"Active\"}",
                result.Value.AfterState);
        }

        [Fact]
        public void TableUpdate_CapturesTheOldAndNewLabel()
        {
            var table = CreateTable("Bar", 3);

            var changes = TenantAuditChangeSets.TableUpdate(table, "Kitchen");

            Assert.False(changes.IsCreate);
            Assert.Equal(new[] { "Label" }, changes.Fields.Select(field => field.Name).ToArray());
            Assert.Equal("Bar", changes.Fields[0].Before);
            Assert.Equal("Kitchen", changes.Fields[0].After);
        }

        [Fact]
        public void TableUpdate_WithNoEffectiveChange_ProducesNoSnapshot()
        {
            var table = CreateTable("Bar", 3);

            var result = TenantAuditSnapshotSerializer.Create(
                "branch_tables",
                TenantAuditChangeSets.TableUpdate(table, "Bar"));

            Assert.True(result.IsSuccess);
            Assert.Null(result.Value.BeforeState);
            Assert.Null(result.Value.AfterState);
        }

        [Fact]
        public void TableLifecycleStatus_CapturesBothDirections()
        {
            var active = CreateTable("Bar", 3);
            var deactivated = TenantAuditSnapshotSerializer.Create(
                "branch_tables",
                TenantAuditChangeSets.TableLifecycleStatus(active, BranchTableStatus.Inactive)).Value;
            Assert.Contains("\"Status\":\"Active\"", deactivated.BeforeState);
            Assert.Contains("\"Status\":\"Inactive\"", deactivated.AfterState);

            active.Deactivate(Now, null);
            var reactivated = TenantAuditSnapshotSerializer.Create(
                "branch_tables",
                TenantAuditChangeSets.TableLifecycleStatus(active, BranchTableStatus.Active)).Value;
            Assert.Contains("\"Status\":\"Inactive\"", reactivated.BeforeState);
            Assert.Contains("\"Status\":\"Active\"", reactivated.AfterState);
        }

        [Fact]
        public void TableBulkCreate_CapturesCountAndRangeOnly()
        {
            var changes = TenantAuditChangeSets.TableBulkCreate(5, 4, 8);

            Assert.True(changes.IsCreate);
            Assert.Equal(
                new[] { "count", "fromNumber", "toNumber" },
                changes.Fields.Select(field => field.Name).ToArray());
            Assert.All(changes.Fields, field => Assert.Null(field.Before));
            Assert.Equal("5", changes.Fields[0].After);
            Assert.Equal("4", changes.Fields[1].After);
            Assert.Equal("8", changes.Fields[2].After);
        }

        [Fact]
        public void TableBulkDeactivate_CapturesCountAndRangeOnly()
        {
            var changes = TenantAuditChangeSets.TableBulkDeactivate(3, 7, 9);

            Assert.False(changes.IsCreate);
            Assert.Equal(
                new[] { "count", "fromNumber", "toNumber" },
                changes.Fields.Select(field => field.Name).ToArray());
            Assert.All(changes.Fields, field => Assert.Null(field.Before));
            Assert.Equal("3", changes.Fields[0].After);
            Assert.Equal("7", changes.Fields[1].After);
            Assert.Equal("9", changes.Fields[2].After);
        }

        private static BranchTable CreateTable(string label, int number) =>
            BranchTable.Create(TableId, BranchId, number, label, "Abcdefghijklmnopqrstuv", Now);
    }
}
