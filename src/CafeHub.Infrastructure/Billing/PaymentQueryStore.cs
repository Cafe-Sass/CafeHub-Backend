using CafeHub.Application.Billing.Contracts;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Billing
{
    internal sealed class PaymentQueryStore(TenantDbContext context) : IPaymentQueryStore
    {
        public async Task<PaymentListPage> ListAsync(
            PaymentListRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var payments = context.Payments.AsNoTracking();
            if (request.BranchId is { } branchId)
            {
                payments = payments.Where(payment => context.Bills.Any(
                    bill => bill.Id == payment.BillId && bill.BranchId == branchId));
            }

            if (request.BillId is { } billId)
            {
                payments = payments.Where(payment => payment.BillId == billId);
            }

            if (request.Status is { } status)
            {
                payments = payments.Where(payment => payment.Status == status);
            }

            int? totalCount = request.IncludeTotalCount
                ? await payments.CountAsync(cancellationToken)
                : null;

            var rows = await payments
                .OrderByDescending(payment => payment.CreatedAt)
                .ThenByDescending(payment => payment.Id)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize + 1)
                .Select(payment => new PaymentListEntry(
                    payment.Id,
                    payment.BillId,
                    payment.Method,
                    payment.Status,
                    payment.Amount,
                    payment.ClaimedAmount,
                    payment.Note,
                    payment.CreatedAt,
                    payment.ConfirmedAt))
                .ToListAsync(cancellationToken);

            return new PaymentListPage(rows, totalCount);
        }

        public async Task<Payment?> FindProofAsync(
            Guid? branchId,
            Guid paymentId,
            CancellationToken cancellationToken)
        {
            var payments = context.Payments.AsNoTracking().Where(payment => payment.Id == paymentId);
            if (branchId is { } branch)
            {
                payments = payments.Where(payment => context.Bills.Any(
                    bill => bill.Id == payment.BillId && bill.BranchId == branch));
            }

            return await payments.SingleOrDefaultAsync(cancellationToken);
        }
    }
}
