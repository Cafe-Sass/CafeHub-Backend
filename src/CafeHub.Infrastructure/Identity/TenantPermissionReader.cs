using CafeHub.Application.Identity;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Identity
{
    internal sealed class TenantPermissionReader(TenantDbContext context) : ITenantPermissionReader
    {
        public async Task<IReadOnlyList<string>> ReadAsync(Guid userId, CancellationToken cancellationToken, Guid? expectedBranchId = null)
        {
            var activeUsers = context.TenantUsers.Where(user => user.Id == userId && user.DeletedAt == null);
            if (expectedBranchId is null && await activeUsers.AnyAsync(user => user.UserKind == TenantUserKind.Owner, cancellationToken))
            {
                return TenantPermissions.All;
            }

            var activeStaff = activeUsers.Where(user => user.UserKind == TenantUserKind.Staff &&
                (expectedBranchId == null || user.BranchId == expectedBranchId) &&
                context.Branches.Any(branch => branch.Id == user.BranchId && branch.LifecycleStatus == BranchLifecycleStatus.Active));

            var roleCodes = from user in activeStaff
                            join membership in context.TenantUserRoles on user.Id equals membership.TenantUserId
                            join role in context.TenantRoles on membership.RoleId equals role.Id
                            where role.DeletedAt == null && role.NormalizedName != TenantRole.NormalizedOwnerName
                            join claim in context.TenantRoleClaims on role.Id equals claim.RoleId
                            where claim.ClaimType == TenantPermissions.ClaimType
                            select claim.ClaimValue;

            var personalCodes = from user in activeStaff
                                join claim in context.TenantUserClaims on user.Id equals claim.TenantUserId
                                where claim.ClaimType == TenantPermissions.ClaimType
                                select claim.ClaimValue;

            return TenantPermissions.StaffUnion(await roleCodes.Union(personalCodes).ToListAsync(cancellationToken));
        }
    }
}
