using CafeHub.Application.Orders.Contracts;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Orders
{
    internal sealed class OrderFeedStore(TenantDbContext context) : IOrderFeedStore
    {
        public async Task<OrderFeedPage> ListFeedAsync(
            OrderFeedListRequest request,
            CancellationToken cancellationToken)
        {
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

            var fetched = await Project(orders
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

            return new OrderFeedPage(fetched, totalCount);
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

            return await Project(orders).SingleOrDefaultAsync(cancellationToken);
        }

        private IQueryable<OrderFeedEntry> Project(IQueryable<Order> orders) => orders
            .Select(order => new OrderFeedEntry(
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
                        item.Addons.Select(addon => addon.AddonName).ToList()))
                    .ToList()));
    }
}
