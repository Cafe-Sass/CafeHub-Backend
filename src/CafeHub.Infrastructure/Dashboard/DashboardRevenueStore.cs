using CafeHub.Application.Dashboard;
using CafeHub.Application.Dashboard.Contracts;
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
                })
                .SingleOrDefaultAsync(cancellationToken);

            var payments = context.Payments.AsNoTracking().WhereConfirmedInRange(request);
            if (request.BranchId is { } branchId)
            {
                payments = payments.Where(payment => context.Bills.Any(
                    bill => bill.Id == payment.BillId && bill.BranchId == branchId));
            }

            var collected = await payments.SumAsync(payment => (decimal?)payment.Amount, cancellationToken) ?? 0m;

            return new DashboardRevenueSnapshot(
                orderTotals?.Sales ?? 0m,
                collected,
                orderTotals?.Orders ?? 0);
        }
    }
}
