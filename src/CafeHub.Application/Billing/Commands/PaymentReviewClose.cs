using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Billing.Contracts;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Billing.Commands
{
    internal static class PaymentReviewClose
    {
        internal static async Task<Result> TryAutoCloseAsync(
            IBillStore billStore,
            Bill bill,
            decimal remainingAmount,
            IReadOnlyList<Payment> payments,
            Guid actorId,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            if (remainingAmount != 0m
                || bill.Status is not BillStatus.Open
                || payments.Any(payment => payment.Status == PaymentStatus.PendingReview))
            {
                return Result.Success();
            }

            bill.Close(null, actorId, now);
            return await billStore.SaveCloseAsync(bill, remainingAmount, actorId, cancellationToken);
        }
    }
}
