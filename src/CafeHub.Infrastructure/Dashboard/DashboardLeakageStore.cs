using CafeHub.Application.Dashboard;
using CafeHub.Application.Dashboard.Contracts;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Dashboard
{
    internal sealed class DashboardLeakageStore(TenantDbContext context) : IDashboardLeakageStore
    {
        public async Task<DashboardLeakageSnapshot> GetAsync(
            DashboardLeakageRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var rejectedAndExpiredOrders = await context.Orders.AsNoTracking()
                .WhereRejectedOrExpiredInRange(request.BranchId, request.From, request.To)
                .Select(order => new
                {
                    order.Id,
                    order.TotalAmount,
                    OccurredAt = order.RespondedAt ?? order.UpdatedAt,
                })
                .ToListAsync(cancellationToken);

            var closedBills = await context.Bills.AsNoTracking()
                .WhereClosedInRange(request.BranchId, request.From, request.To)
                .Select(bill => new
                {
                    bill.Id,
                    TotalAmount = context.Orders
                        .Where(order => order.BillId == bill.Id && order.Status == OrderStatus.Accepted)
                        .Sum(order => (decimal?)order.TotalAmount) ?? 0m,
                    PaidAmount = context.Payments
                        .Where(payment => payment.BillId == bill.Id && payment.Status == PaymentStatus.Confirmed)
                        .Sum(payment => (decimal?)payment.Amount) ?? 0m,
                    bill.ClosedAt,
                })
                .ToListAsync(cancellationToken);

            var rejectedPaymentProofs = await context.Payments.AsNoTracking()
                .WhereRejectedInRange(request.From, request.To)
                .Where(payment => request.BranchId == null || context.Bills.Any(
                    bill => bill.Id == payment.BillId && bill.BranchId == request.BranchId))
                .Select(payment => new
                {
                    payment.Id,
                    Amount = payment.ClaimedAmount ?? payment.Amount,
                    payment.UpdatedAt,
                })
                .ToListAsync(cancellationToken);

            return new DashboardLeakageSnapshot(
                rejectedAndExpiredOrders
                    .Select(row => new DashboardLeakageRecord(row.Id, row.TotalAmount, row.OccurredAt))
                    .ToList(),
                closedBills
                    .Select(row => new DashboardLeakageBillRecord(
                        row.Id,
                        row.TotalAmount,
                        row.PaidAmount,
                        row.ClosedAt!.Value))
                    .ToList(),
                rejectedPaymentProofs
                    .Select(row => new DashboardLeakageRecord(row.Id, row.Amount, row.UpdatedAt))
                    .ToList());
        }
    }
}
