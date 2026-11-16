using CafeHub.Application.Stations.Contracts;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Stations
{
    internal sealed class StationScope(TenantDbContext context) : IStationScope
    {
        public async Task<IReadOnlyList<Guid>?> GetVisibleStationIdsAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            var branchId = await context.TenantUsers
                .Where(u => u.Id == userId && u.UserKind == TenantUserKind.Staff)
                .Select(u => u.BranchId)
                .FirstOrDefaultAsync(cancellationToken);

            if (branchId is null)
            {
                return null;
            }

            var hasAssignments = await context.TenantUserStations
                .AnyAsync(link => link.TenantUserId == userId, cancellationToken);

            if (!hasAssignments)
            {
                return null;
            }

            return await context.TenantUserStations
                .Where(link => link.TenantUserId == userId)
                .Join(
                    context.Stations.Where(s => s.Status == StationStatus.Active && s.BranchId == branchId),
                    link => link.StationId,
                    station => station.Id,
                    (link, station) => station.Id)
                .Distinct()
                .OrderBy(id => id)
                .ToListAsync(cancellationToken);
        }
    }
}
