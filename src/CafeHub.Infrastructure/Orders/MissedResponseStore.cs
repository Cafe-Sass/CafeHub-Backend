using CafeHub.Application.Identity;
using CafeHub.Application.Orders.Contracts;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Orders
{
    internal sealed class MissedResponseStore(TenantDbContext context) : IMissedResponseStore
    {
        public async Task<MissedResponseSummaryPage> ListSummaryAsync(
            MissedResponseSummaryRequest request,
            CancellationToken cancellationToken)
        {
            var rangeStart = new DateTimeOffset(request.From.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            var rangeEnd = new DateTimeOffset(request.To.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

            var misses = context.OrderResponseMisses
                .AsNoTracking()
                .Where(miss => miss.BranchId == request.BranchId
                    && miss.ExpiredAt >= rangeStart
                    && miss.ExpiredAt < rangeEnd);

            int? totalCount = request.IncludeTotalCount
                ? await context.OrderResponseMissHolders
                    .AsNoTracking()
                    .Where(holder => context.OrderResponseMisses.Any(miss =>
                        miss.Id == holder.OrderResponseMissId
                        && miss.BranchId == request.BranchId
                        && miss.ExpiredAt >= rangeStart
                        && miss.ExpiredAt < rangeEnd))
                    .CountAsync(cancellationToken)
                : null;

            var rows = await misses
                .Join(
                    context.OrderResponseMissHolders.AsNoTracking(),
                    miss => miss.Id,
                    holder => holder.OrderResponseMissId,
                    (miss, holder) => new { holder.UserId })
                .Join(
                    context.TenantUsers.AsNoTracking(),
                    holder => holder.UserId,
                    user => user.Id,
                    (holder, user) => new { user.Id, user.FullName })
                .GroupBy(row => new { row.Id, row.FullName })
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key.FullName)
                .ThenBy(group => group.Key.Id)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize + 1)
                .Select(group => new MissedResponseSummaryEntry(group.Key.Id, group.Key.FullName, group.Count()))
                .ToListAsync(cancellationToken);

            return new MissedResponseSummaryPage(
                rows.Take(request.PageSize).ToList(),
                totalCount);
        }
    }
}
