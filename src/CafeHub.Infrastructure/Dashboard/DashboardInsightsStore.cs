using CafeHub.Application.Dashboard;
using CafeHub.Application.Dashboard.Contracts;
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

            return new DashboardInsightsSnapshot(productSales, acceptedOrderTimes);
        }
    }
}
