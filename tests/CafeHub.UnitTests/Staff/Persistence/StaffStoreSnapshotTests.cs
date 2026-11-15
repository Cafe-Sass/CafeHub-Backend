using CafeHub.Application.Audit;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;

namespace CafeHub.UnitTests.Staff.Persistence
{
    public sealed class StaffStoreSnapshotTests
    {
        private static readonly Guid BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        [Fact]
        public void StaffCreate_CapturesApprovedNewValuesOnly()
        {
            var user = CreateStaffUser("Jane Doe");
            var branch = CreateBranch();

            var changes = TenantAuditChangeSets.StaffCreate(user, branch);

            Assert.True(changes.IsCreate);
            Assert.Equal(
                new[] { "FullName", "BranchId", "IsActive" },
                changes.Fields.Select(f => f.Name).ToArray());
            Assert.All(changes.Fields, f => Assert.Null(f.Before));
            Assert.Equal("Jane Doe", changes.Fields[0].After);
            Assert.Equal(BranchId.ToString(), changes.Fields[1].After);
            Assert.Equal("True", changes.Fields[2].After);
        }

        [Fact]
        public void StaffCreate_NeverContainsPasswordEmailOrPhone()
        {
            var user = CreateStaffUser("Jane Doe");
            var branch = CreateBranch();

            var changes = TenantAuditChangeSets.StaffCreate(user, branch);

            Assert.DoesNotContain(changes.Fields, f =>
                f.Name.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                f.Name.Contains("email", StringComparison.OrdinalIgnoreCase) ||
                f.Name.Contains("phone", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void StaffCreate_SnapshotSerializer_ProducesBeforeNullAfterJson()
        {
            var user = CreateStaffUser("Jane Doe");
            var branch = CreateBranch();

            var changes = TenantAuditChangeSets.StaffCreate(user, branch);
            var result = TenantAuditSnapshotSerializer.Create("tenant_users", changes);

            Assert.True(result.IsSuccess);
            Assert.Null(result.Value.BeforeState);
            Assert.NotNull(result.Value.AfterState);
            Assert.Contains("FullName", result.Value.AfterState);
            Assert.Contains("BranchId", result.Value.AfterState);
            Assert.Contains("IsActive", result.Value.AfterState);
        }

        [Fact]
        public void StaffUpdate_CapturesApprovedOldAndNewValues()
        {
            var branchId2 = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
            var user = CreateStaffUser("Jane Doe");

            var changes = TenantAuditChangeSets.StaffUpdate(user, "Jane Smith", branchId2);

            Assert.False(changes.IsCreate);
            Assert.Equal(new[] { "FullName", "BranchId" }, changes.Fields.Select(f => f.Name).ToArray());
            Assert.Equal("Jane Doe", changes.Fields[0].Before);
            Assert.Equal("Jane Smith", changes.Fields[0].After);
            Assert.Equal(BranchId.ToString(), changes.Fields[1].Before);
            Assert.Equal(branchId2.ToString(), changes.Fields[1].After);
        }

        [Fact]
        public void StaffUpdate_NeverContainsPasswordEmailOrPhone()
        {
            var user = CreateStaffUser("Jane Doe");

            var changes = TenantAuditChangeSets.StaffUpdate(user, "Jane Smith", Guid.NewGuid());

            Assert.DoesNotContain(changes.Fields, f =>
                f.Name.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                f.Name.Contains("email", StringComparison.OrdinalIgnoreCase) ||
                f.Name.Contains("phone", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void StaffUpdate_WithSameValuesProducesNoSnapshotPayload()
        {
            var user = CreateStaffUser("Jane Doe");

            var changes = TenantAuditChangeSets.StaffUpdate(user, user.FullName, BranchId);
            var result = TenantAuditSnapshotSerializer.Create("tenant_users", changes);

            Assert.True(result.IsSuccess);
            Assert.Null(result.Value.BeforeState);
            Assert.Null(result.Value.AfterState);
        }

        [Fact]
        public void StaffLifecycleStatus_Deactivate_CapturesIsActiveTrueToFalse()
        {
            var user = CreateStaffUser("Jane Doe");

            var changes = TenantAuditChangeSets.StaffLifecycleStatus(user, false);

            Assert.False(changes.IsCreate);
            var field = Assert.Single(changes.Fields);
            Assert.Equal("IsActive", field.Name);
            Assert.Equal("True", field.Before);
            Assert.Equal("False", field.After);
        }

        [Fact]
        public void StaffLifecycleStatus_Reactivate_CapturesIsActiveFalseToTrue()
        {
            var user = CreateStaffUser("Jane Doe");
            user.Deactivate(Now, Guid.NewGuid());

            var changes = TenantAuditChangeSets.StaffLifecycleStatus(user, true);

            Assert.False(changes.IsCreate);
            var field = Assert.Single(changes.Fields);
            Assert.Equal("IsActive", field.Name);
            Assert.Equal("False", field.Before);
            Assert.Equal("True", field.After);
        }

        [Fact]
        public void StaffLifecycleStatus_SnapshotSerializer_ProducesBeforeAndAfterJson()
        {
            var user = CreateStaffUser("Jane Doe");

            var changes = TenantAuditChangeSets.StaffLifecycleStatus(user, false);
            var result = TenantAuditSnapshotSerializer.Create("tenant_users", changes);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Value.BeforeState);
            Assert.NotNull(result.Value.AfterState);
            Assert.Contains("IsActive", result.Value.BeforeState);
            Assert.Contains("IsActive", result.Value.AfterState);
        }

        [Fact]
        public void TenantUsersAllowList_BlocksEmailPhonePassword()
        {
            var fields = new[]
            {
                new TenantAuditChanges.FieldChange("Email", "old@test.com", "new@test.com"),
                new TenantAuditChanges.FieldChange("Phone", "old", "new"),
                new TenantAuditChanges.FieldChange("PasswordHash", "old", "new"),
            };
            var changes = TenantAuditChanges.Update(fields);
            var result = TenantAuditSnapshotSerializer.Create("tenant_users", changes);

            Assert.True(result.IsSuccess);
            Assert.Null(result.Value.BeforeState);
            Assert.Null(result.Value.AfterState);
        }

        [Fact]
        public void TenantUsersAllowList_AllowsOnlyBranchIdFullNameIsActive()
        {
            var fields = new[]
            {
                new TenantAuditChanges.FieldChange("FullName", "old", "new"),
                new TenantAuditChanges.FieldChange("BranchId", Guid.Empty.ToString(), BranchId.ToString()),
                new TenantAuditChanges.FieldChange("IsActive", "True", "False"),
                new TenantAuditChanges.FieldChange("UserName", "old", "new"),
            };
            var changes = TenantAuditChanges.Update(fields);
            var result = TenantAuditSnapshotSerializer.Create("tenant_users", changes);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Value.BeforeState);
            Assert.Contains("FullName", result.Value.BeforeState);
            Assert.Contains("BranchId", result.Value.BeforeState);
            Assert.Contains("IsActive", result.Value.BeforeState);
            Assert.DoesNotContain("UserName", result.Value.BeforeState!);
        }

        private static TenantUser CreateStaffUser(string fullName)
        {
            var id = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
            return TenantUser.CreateStaff(
                id,
                "jane-doe-eeeee",
                "JANE-DOE-EEEEE",
                fullName,
                "jane@test.com",
                "JANE@TEST.COM",
                "+966500000001",
                "placeholder-hash",
                Guid.NewGuid().ToString("N"),
                Guid.NewGuid().ToString("N"),
                BranchId,
                Now);
        }

        private static Branch CreateBranch() => Branch.Create(
            BranchId,
            "Main Branch",
            BranchLifecycleStatus.Active,
            null,
            null,
            "Daily 9-5",
            Now);
    }
}
