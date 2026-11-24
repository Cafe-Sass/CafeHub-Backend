using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Billing.Contracts;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Billing.Commands
{
    internal static class BillCompletion
    {
        internal static async Task<Result> TryCompleteAsync(
            IBillStore billStore,
            IReceiptIssuer receiptIssuer,
            Bill bill,
            IReadOnlyList<Order> acceptedOrders,
            IReadOnlyList<Payment> payments,
            Guid actorId,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            var remainingAmount = Bill.ResolveTotalAmount(acceptedOrders) - Payment.ResolvePaidAmount(payments);
            if (remainingAmount != 0m
                || bill.Status is not BillStatus.Open
                || payments.Any(payment => payment.Status == PaymentStatus.PendingReview))
            {
                return Result.Success();
            }

            bill.Close(null, actorId, now);
            var closed = await billStore.SaveCloseAsync(bill, remainingAmount, actorId, cancellationToken);
            if (closed.IsFailure)
            {
                return closed;
            }

            return await receiptIssuer.IssueAsync(
                bill,
                acceptedOrders,
                payments,
                actorId,
                now,
                cancellationToken);
        }
    }
}
