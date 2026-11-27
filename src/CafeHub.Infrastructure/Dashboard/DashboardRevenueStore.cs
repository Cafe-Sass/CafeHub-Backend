using CafeHub.Application.Dashboard;
using CafeHub.Application.Dashboard.Contracts;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Dashboard
{
    internal sealed class DashboardRevenueStore(TenantDbContext context) : IDashboardRevenueStore
    {
        public async Task<DashboardRevenueSnapshot> GetAsync(
            DashboardRevenueRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var orderTotals = await context.Orders.AsNoTracking()
                .WhereAcceptedInRange(request)
                .GroupBy(order => 1)
                .Select(group => new
                {
                    Sales = group.Sum(order => order.TotalAmount),
                    Orders = group.Count(),
                    NonWifiOrders = group.Sum(
                        order => order.ServiceType == OrderServiceType.WifiOnly ? 0 : 1),
                })
                .SingleOrDefaultAsync(cancellationToken);

            var payments = context.Payments.AsNoTracking().WhereConfirmedInRange(request);
            if (request.BranchId is { } branchId)
            {
                payments = payments.Where(payment => context.Bills.Any(
                    bill => bill.Id == payment.BillId && bill.BranchId == branchId));
            }

            var methodTotals = await payments
                .GroupBy(payment => payment.Method)
                .Select(group => new { Method = group.Key, Amount = group.Sum(payment => payment.Amount) })
                .ToListAsync(cancellationToken);

            return new DashboardRevenueSnapshot(
                orderTotals?.Sales ?? 0m,
                methodTotals.Sum(row => row.Amount),
                orderTotals?.Orders ?? 0,
                orderTotals?.NonWifiOrders ?? 0,
                methodTotals.Where(row => row.Method == PaymentMethod.Cash).Sum(row => row.Amount),
                methodTotals.Where(row => row.Method == PaymentMethod.Online).Sum(row => row.Amount));
        }
    }
}
