using CafeHub.Application.Orders.Contracts;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Orders
{
    internal sealed class OrderFeedStore(TenantDbContext context, TimeProvider timeProvider) : IOrderFeedStore
    {
        public async Task<OrderFeedPage> ListFeedAsync(
            OrderFeedListRequest request,
            CancellationToken cancellationToken)
        {
            var now = timeProvider.GetUtcNow();
            var orders = context.Orders.AsNoTracking()
                .Where(order => order.BranchId == request.BranchId);

            if (request.Status is { } status)
            {
                orders = orders.Where(order => order.Status == status);
            }

            if (request.UpdatedSince is { } updatedSince)
            {
                orders = orders.Where(order => order.UpdatedAt > updatedSince);
            }

            if (request.StationIds is not null)
            {
                var stationIds = request.StationIds;
                orders = orders.Where(order => order.Items.Any(item =>
                    item.StationId == null || stationIds.Contains(item.StationId.Value)));
            }

            if (!request.IncludeMuted)
            {
                orders = orders.Where(order => order.GuestDeviceId == null
                    || !context.GuestMutes.Any(mute => mute.BranchId == order.BranchId
                        && mute.DeviceId == order.GuestDeviceId
                        && mute.LiftedAt == null
                        && mute.ExpiresAt > now));
            }

            var rows = await Project(orders
                    .OrderByDescending(order => order.SubmittedAt)
                    .ThenByDescending(order => order.Id)
                    .Skip((request.Page - 1) * request.PageSize)
                    .Take(request.PageSize + 1))
                .ToListAsync(cancellationToken);

            int? totalCount = null;
            if (request.IncludeTotalCount)
            {
                totalCount = await orders.CountAsync(cancellationToken);
            }

            var entries = await WithMuteSuggestionsAsync(request.BranchId, rows, now, cancellationToken);
            return new OrderFeedPage(entries, totalCount);
        }

        public async Task<OrderFeedEntry?> FindByIdAsync(
            Guid? branchId,
            Guid orderId,
            CancellationToken cancellationToken)
        {
            var orders = context.Orders.AsNoTracking().Where(order => order.Id == orderId);
            if (branchId is { } branch)
            {
                orders = orders.Where(order => order.BranchId == branch);
            }

            var row = await Project(orders).SingleOrDefaultAsync(cancellationToken);
            if (row is null)
            {
                return null;
            }

            var entries = await WithMuteSuggestionsAsync(
                row.BranchId, [row], timeProvider.GetUtcNow(), cancellationToken);
            return entries[0];
        }

        private async Task<IReadOnlyList<OrderFeedEntry>> WithMuteSuggestionsAsync(
            Guid branchId,
            IReadOnlyList<FeedRow> rows,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            var deviceIds = rows
                .Select(row => row.DeviceId)
                .OfType<string>()
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (deviceIds.Length == 0)
            {
                return [.. rows.Select(row => row.Entry)];
            }

            var rejectedSince = now.AddHours(-GuestMute.RejectionWindowHours);
            var rejectedCounts = await context.Orders.AsNoTracking()
                .Where(order => order.BranchId == branchId
                    && order.GuestDeviceId != null
                    && deviceIds.Contains(order.GuestDeviceId)
                    && order.Status == OrderStatus.Rejected
                    && order.RespondedAt != null
                    && order.RespondedAt > rejectedSince)
                .GroupBy(order => order.GuestDeviceId!)
                .Select(group => new { DeviceId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(row => row.DeviceId, row => row.Count, cancellationToken);

            var mutedDeviceIds = await context.GuestMutes.AsNoTracking()
                .Where(mute => mute.BranchId == branchId
                    && mute.LiftedAt == null
                    && mute.ExpiresAt > now
                    && deviceIds.Contains(mute.DeviceId))
                .Select(mute => mute.DeviceId)
                .Distinct()
                .ToListAsync(cancellationToken);

            return [.. rows.Select(row => row.Entry with
            {
                SuggestMute = row.DeviceId is { } deviceId
                    && !mutedDeviceIds.Contains(deviceId, StringComparer.Ordinal)
                    && GuestMute.ShouldSuggestMute(rejectedCounts.GetValueOrDefault(deviceId)),
            })];
        }

        private IQueryable<FeedRow> Project(IQueryable<Order> orders) => orders
            .Select(order => new FeedRow(
                new OrderFeedEntry(
                    order.Id,
                    order.OrderNumber,
                    context.BranchTables
                        .Where(table => table.Id == order.TableId)
                        .Select(table => table.Number)
                        .FirstOrDefault(),
                    order.ServiceType.ToString(),
                    order.Status.ToString(),
                    order.GuestDisplayName,
                    order.SubmittedAt,
                    order.ResponseDeadline,
                    order.UpdatedAt,
                    order.TotalAmount,
                    order.Items
                        .OrderBy(item => item.LineNumber)
                        .Select(item => new OrderFeedEntryItem(
                            item.ProductName,
                            item.SizeName,
                            item.Quantity,
                            item.StationId,
                            context.Stations
                                .Where(station => station.Id == item.StationId)
                                .Select(station => (string?)station.Name)
                                .FirstOrDefault(),
                            item.Addons.OrderBy(addon => addon.Id).Select(addon => addon.AddonName).ToList()))
                        .ToList()),
                order.BranchId,
                order.GuestDeviceId));

        private sealed record FeedRow(OrderFeedEntry Entry, Guid BranchId, string? DeviceId);
    }
}
