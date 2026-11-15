using System.Data.Common;
using CafeHub.Application.Identity;
using CafeHub.Infrastructure.Identity;

namespace CafeHub.UnitTests
{
    public sealed class TenantPermissionReaderTests
    {
        [Fact]
        public async Task StaffPermissionQueryBindsBothGrantSourcesToTheActiveAccountAndCapturedBranch()
        {
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var commands = new List<string>();
            var stub = new TenantCommandStub((command, _) =>
            {
                var query = command.CommandText;
                commands.Add(query);
                var parameters = command.Parameters.Cast<DbParameter>().ToArray();
                Assert.Contains(parameters, parameter => Equals(parameter.Value, userId));
                var branch = Assert.Single(parameters, parameter => Equals(parameter.Value, branchId));
                var grantSources = query.Split("UNION", StringSplitOptions.TrimEntries);
                Assert.Equal(2, grantSources.Length);
                foreach (var source in grantSources)
                {
                    Assert.Contains("deleted_at IS NULL", source, StringComparison.Ordinal);
                    Assert.Contains("user_kind = 'Staff'", source, StringComparison.Ordinal);
                    Assert.Contains($"branch_id = @{branch.ParameterName.TrimStart('@')}", source, StringComparison.Ordinal);
                    Assert.Contains("FROM branches", source, StringComparison.Ordinal);
                    Assert.Contains("lifecycle_status = 'Active'", source, StringComparison.Ordinal);
                }

                Assert.Contains("tenant_role_claims", grantSources[0], StringComparison.Ordinal);
                Assert.Contains("normalized_name", grantSources[0], StringComparison.Ordinal);
                Assert.Contains("tenant_user_claims", grantSources[1], StringComparison.Ordinal);
                return TenantCommandStub.Scalars(TenantPermissions.StaffRead, TenantPermissions.StaffManage,
                    TenantPermissions.RolesManage, TenantPermissions.BranchesManage, TenantPermissions.CafeProfileManage, "unknown");
            });
            await using var context = stub.CreateContext();

            var permissions = await new TenantPermissionReader(context).ReadAsync(userId, default, branchId);

            Assert.Equal(new[] { TenantPermissions.StaffManage, TenantPermissions.StaffRead }, permissions);
            Assert.Single(commands);
        }

        [Fact]
        public async Task AStaffAccountExcludedByTheActiveAccountAndBranchQueryHasNoPermissions()
        {
            var stub = new TenantCommandStub((command, _) =>
            {
                Assert.Contains("deleted_at IS NULL", command.CommandText, StringComparison.Ordinal);
                Assert.Contains("lifecycle_status = 'Active'", command.CommandText, StringComparison.Ordinal);
                Assert.Contains("branch_id = @", command.CommandText, StringComparison.Ordinal);
                return TenantCommandStub.Scalars<string>();
            });
            await using var context = stub.CreateContext();

            Assert.Empty(await new TenantPermissionReader(context).ReadAsync(Guid.NewGuid(), default, Guid.NewGuid()));
        }

        [Fact]
        public async Task ActiveOwnerReceivesAllCatalogPermissions()
        {
            var stub = new TenantCommandStub((command, _) =>
            {
                Assert.Contains("deleted_at IS NULL", command.CommandText, StringComparison.Ordinal);
                Assert.Contains("user_kind = 'Owner'", command.CommandText, StringComparison.Ordinal);
                return TenantCommandStub.Scalars(true);
            });
            await using var context = stub.CreateContext();

            Assert.Equal(TenantPermissions.All, await new TenantPermissionReader(context).ReadAsync(Guid.NewGuid(), default));
        }
    }
}
