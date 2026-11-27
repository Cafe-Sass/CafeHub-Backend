using CafeHub.Application.Dashboard;
using CafeHub.Application.Dashboard.Contracts;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Dashboard
{
    internal sealed class DashboardInsightsStore(TenantDbContext context) : IDashboardInsightsStore
    {
        public async Task<DashboardInsightsSnapshot> GetAsync(
            DashboardInsightsRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var acceptedOrders = context.Orders.AsNoTracking()
                .WhereAcceptedInRange(request.BranchId, request.From, request.To);

            var productTotals = await acceptedOrders
                .SelectMany(order => order.Items)
                .Where(item => item.ProductId != null)
                .GroupBy(item => new { item.ProductId, item.ProductName })
                .Select(group => new
                {
                    group.Key.ProductId,
                    group.Key.ProductName,
                    Quantity = group.Sum(item => item.Quantity),
                    Sales = group.Sum(item => item.LineTotal),
                })
                .ToListAsync(cancellationToken);

            var productSales = productTotals
                .Select(row => new DashboardProductSalesRow(
                    row.ProductId!.Value,
                    row.ProductName,
                    row.Quantity,
                    row.Sales))
                .ToList();

            var acceptedOrderTimes = await acceptedOrders
                .Select(order => order.RespondedAt ?? order.SubmittedAt)
                .ToListAsync(cancellationToken);

            var dineInOrders = acceptedOrders.Where(order => order.ServiceType == OrderServiceType.DineIn);
            var acceptedDineInOrders = await dineInOrders.CountAsync(cancellationToken);
            var acceptedDineInOrdersWithWifiPackage = await dineInOrders
                .Where(order => order.Items.Any(item => item.WifiPackageId != null))
                .CountAsync(cancellationToken);

            var openNearExpiry = await context.InventoryAlerts.AsNoTracking()
                .Where(alert => alert.State == InventoryAlertState.Open
                    && alert.Kind == InventoryAlertKind.NearExpiry
                    && (request.BranchId == null || alert.BranchId == request.BranchId))
                .Join(
                    context.InventoryItems.AsNoTracking(),
                    alert => alert.ItemId,
                    item => item.Id,
                    (alert, item) => new { Alert = alert, Item = item })
                .Join(
                    context.InventoryBatches.AsNoTracking(),
                    pair => pair.Alert.BatchId,
                    batch => (Guid?)batch.Id,
                    (pair, batch) => new { pair.Alert, pair.Item, Batch = batch })
                .Where(row => row.Batch.ExpiryDate != null)
                .Select(row => new
                {
                    ItemId = row.Item.Id,
                    ItemName = row.Item.Name,
                    row.Alert.BranchId,
                    BatchId = row.Batch.Id,
                    row.Batch.ExpiryDate,
                })
                .ToListAsync(cancellationToken);

            var nearExpiryItems = openNearExpiry
                .Select(row => new DashboardNearExpiryRow(
                    row.ItemId,
                    row.ItemName,
                    row.BranchId,
                    row.BatchId,
                    row.ExpiryDate!.Value))
                .ToList();

            return new DashboardInsightsSnapshot(
                productSales,
                acceptedOrderTimes,
                acceptedDineInOrders,
                acceptedDineInOrdersWithWifiPackage,
                nearExpiryItems);
        }
    }
}
