using CafeHub.Application.Refunds.Contracts;
using CafeHub.Application.Refunds.DTOs;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Refunds
{
    internal sealed class RefundRequestStore(TenantDbContext context) : IRefundRequestStore
    {
        public async Task<IReadOnlyList<GuestRefundablePayment>?> ListConfirmedPaymentsForBillAsync(
            string billReference,
            CancellationToken cancellationToken)
        {
            var billId = await context.Bills.AsNoTracking()
                .Where(bill => bill.PublicReference == billReference)
                .Select(bill => (Guid?)bill.Id)
                .SingleOrDefaultAsync(cancellationToken);
            if (billId is null)
            {
                return null;
            }

            var payments = await context.Payments.AsNoTracking()
                .Where(payment => payment.BillId == billId && payment.Status == PaymentStatus.Confirmed)
                .OrderBy(payment => payment.ConfirmedAt)
                .ThenBy(payment => payment.Id)
                .Select(payment => new
                {
                    payment.Id,
                    payment.Method,
                    payment.Amount,
                    payment.ConfirmedAt,
                })
                .ToListAsync(cancellationToken);

            return payments.Select(payment => new GuestRefundablePayment(
                    payment.Id,
                    payment.Method.ToString(),
                    payment.Amount,
                    payment.ConfirmedAt!.Value))
                .ToArray();
        }

        public async Task<GuestRefundRequestStatus?> FindGuestStatusAsync(
            string publicReference,
            CancellationToken cancellationToken)
        {
            var request = await context.RefundRequests.AsNoTracking()
                .Where(candidate => candidate.PublicReference == publicReference)
                .Select(candidate => new
                {
                    candidate.PublicReference,
                    candidate.Status,
                    candidate.ResolutionNote,
                })
                .SingleOrDefaultAsync(cancellationToken);

            return request is null
                ? null
                : new GuestRefundRequestStatus(
                    request.PublicReference,
                    request.Status.ToString(),
                    request.Status is RefundRequestStatus.Approved or RefundRequestStatus.Rejected
                        ? request.ResolutionNote
                        : null);
        }
    }
}
