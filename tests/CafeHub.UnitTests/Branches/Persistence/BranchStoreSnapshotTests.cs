using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;

namespace CafeHub.UnitTests.Branches.Persistence
{
    public sealed class BranchStoreSnapshotTests
    {
        [Fact]
        public void BranchCreate_CapturesApprovedNewValuesOnly()
        {
            var branch = CreateBranch();

            var changes = TenantAuditChangeSets.BranchCreate(branch);

            Assert.True(changes.IsCreate);
            Assert.Equal(
                new[] { "Name", "OpeningHoursSummary", "LifecycleStatus" },
                changes.Fields.Select(field => field.Name).ToArray());
            Assert.All(changes.Fields, field => Assert.Null(field.Before));
            Assert.Equal("Branch", changes.Fields[0].After);
            Assert.Equal("Daily", changes.Fields[1].After);
            Assert.Equal("Active", changes.Fields[2].After);
        }

        [Fact]
        public void BranchUpdate_CapturesApprovedOldAndNewValuesOnly()
        {
            var branch = CreateBranch();

            var changes = TenantAuditChangeSets.BranchUpdate(branch, "New Branch", "Weekends");

            Assert.False(changes.IsCreate);
            Assert.Equal(new[] { "Name", "OpeningHoursSummary" }, changes.Fields.Select(field => field.Name).ToArray());
            Assert.Equal("Branch", changes.Fields[0].Before);
            Assert.Equal("New Branch", changes.Fields[0].After);
            Assert.Equal("Daily", changes.Fields[1].Before);
            Assert.Equal("Weekends", changes.Fields[1].After);
            Assert.DoesNotContain(changes.Fields, field => field.Name is "Address" or "ContactPhone");
        }

        [Fact]
        public void BranchUpdate_WithSameAllowedValuesProducesNoSnapshotPayload()
        {
            var branch = CreateBranch();

            var changes = TenantAuditChangeSets.BranchUpdate(branch, branch.Name, branch.OpeningHoursSummary);
            var result = TenantAuditSnapshotSerializer.Create("branches", changes);

            Assert.True(result.IsSuccess);
            Assert.Null(result.Value.BeforeState);
            Assert.Null(result.Value.AfterState);
        }

        [Theory]
        [InlineData(BranchLifecycleStatus.Active, BranchLifecycleStatus.Suspended)]
        [InlineData(BranchLifecycleStatus.Suspended, BranchLifecycleStatus.Active)]
        [InlineData(BranchLifecycleStatus.Active, BranchLifecycleStatus.Retired)]
        [InlineData(BranchLifecycleStatus.Suspended, BranchLifecycleStatus.Retired)]
        public void BranchLifecycleStatus_CapturesActualOldAndNewStatus(
            BranchLifecycleStatus oldStatus,
            BranchLifecycleStatus newStatus)
        {
            var branch = CreateBranch(oldStatus);

            var changes = TenantAuditChangeSets.BranchLifecycleStatus(branch, newStatus);

            var field = Assert.Single(changes.Fields);
            Assert.False(changes.IsCreate);
            Assert.Equal("LifecycleStatus", field.Name);
            Assert.Equal(oldStatus.ToString(), field.Before);
            Assert.Equal(newStatus.ToString(), field.After);
        }

        private static Branch CreateBranch(BranchLifecycleStatus status = BranchLifecycleStatus.Active) => Branch.Create(
            Guid.NewGuid(),
            "Branch",
            status,
            "excluded-address",
            "excluded-phone",
            "Daily",
            DateTimeOffset.UtcNow);
    }
}
