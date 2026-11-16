using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Persistence.Tenant
{
    internal static class TenantRowLocks
    {
        internal static Task LockUserAsync(this TenantDbContext context, Guid id, CancellationToken cancellationToken) =>
            context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT id FROM tenant_users WHERE id = {id} FOR UPDATE", cancellationToken);

        internal static Task LockRoleAsync(this TenantDbContext context, Guid id, CancellationToken cancellationToken) =>
            context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT id FROM tenant_roles WHERE id = {id} FOR UPDATE", cancellationToken);

        internal static Task LockMenuCategoryAsync(this TenantDbContext context, Guid id, CancellationToken cancellationToken) =>
            context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT id FROM menu_categories WHERE id = {id} FOR UPDATE", cancellationToken);

        internal static async Task<bool> LockBranchStaffAsync(
            this TenantDbContext context, Guid id, Guid branchId, CancellationToken cancellationToken)
        {
            var locked = await context.Database.SqlQuery<Guid>(
                    $"SELECT id AS \"Value\" FROM tenant_users WHERE id = {id} AND branch_id = {branchId} AND user_kind = 'Staff' FOR UPDATE")
                .ToListAsync(cancellationToken);
            return locked.Count == 1;
        }

        internal static async Task LockBranchesAsync(this TenantDbContext context, CancellationToken cancellationToken, params Guid[] ids)
        {
            foreach (var id in ids.Distinct().Order())
            {
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT id FROM branches WHERE id = {id} FOR UPDATE", cancellationToken);
            }
        }
    }
}
