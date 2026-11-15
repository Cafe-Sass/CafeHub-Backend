using CafeHub.Application.Identity;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Identity
{
    internal sealed class TenantAccountReader(TenantDbContext context) : ITenantAccountReader
    {
        public async Task<TenantAccountAccess?> FindActiveAsync(Guid userId, CancellationToken cancellationToken)
        {
            var account = await context.TenantUsers
                .Where(user => user.Id == userId && user.DeletedAt == null)
                .Select(user => new
                {
                    user.UserKind,
                    user.SecurityStamp,
                    HasActiveBranch = context.Branches.Any(branch =>
                        branch.Id == user.BranchId && branch.LifecycleStatus == BranchLifecycleStatus.Active),
                })
                .SingleOrDefaultAsync(cancellationToken);

            return account is null ? null : new TenantAccountAccess(
                account.UserKind == TenantUserKind.Staff,
                TenantSessionVersion.FromStamp(account.SecurityStamp),
                account.HasActiveBranch);
        }

        internal Task LockAsync(Guid userId, CancellationToken cancellationToken) =>
            context.LockUserAsync(userId, cancellationToken);
    }
}
