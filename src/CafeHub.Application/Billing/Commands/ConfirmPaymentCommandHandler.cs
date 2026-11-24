using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Billing.Contracts;
using CafeHub.Application.Billing.DTOs;
using CafeHub.Application.Identity;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Billing.Commands
{
    public sealed class ConfirmPaymentCommandHandler(
        IPaymentStore store,
        IBillStore billStore,
        IReceiptIssuer receiptIssuer,
        ICurrentTenantUser currentUser,
        TimeProvider timeProvider)
        : ICommandHandler<ConfirmPaymentCommand, ConfirmPaymentResult>
    {
        public async Task<Result<ConfirmPaymentResult>> Handle(
            ConfirmPaymentCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<ConfirmPaymentResult>(scope.Error!);
            }

            if (currentUser.UserId is not { } actorId || actorId == Guid.Empty)
            {
                return Result.Failure<ConfirmPaymentResult>(AuthErrors.Forbidden());
            }

            var review = await store.FindReviewAsync(scope.Value, command.PaymentId, cancellationToken);
            if (review is null
                || review.Payment.Method is not PaymentMethod.Online
                || review.Payment.ClaimedAmount is null)
            {
                return Result.Failure<ConfirmPaymentResult>(BillingErrors.PaymentNotFound());
            }

            var payment = review.Payment;
            if (payment.Status is PaymentStatus.Rejected)
            {
                return Result.Failure<ConfirmPaymentResult>(
                    BillingErrors.PaymentNotPendingReview(payment.Status));
            }

            var settlement = review.Settlement;
            var billTotal = Bill.ResolveTotalAmount(settlement.AcceptedOrders);
            if (payment.Status is PaymentStatus.Confirmed)
            {
                return Result.Success(BuildResult(settlement, payment, billTotal));
            }

            var paidBefore = Payment.ResolvePaidAmount(settlement.Payments);
            var remaining = billTotal - paidBefore;
            var amount = command.Amount ?? payment.ClaimedAmount!.Value;
            if (amount > remaining)
            {
                return Result.Failure<ConfirmPaymentResult>(BillingErrors.AmountAboveRemaining());
            }

            var now = timeProvider.GetUtcNow();
            payment.ConfirmReview(amount, actorId, now);
            var saved = await store.SaveReviewAsync(payment, null, cancellationToken);
            if (saved.IsFailure)
            {
                return Result.Failure<ConfirmPaymentResult>(saved.Error!);
            }

            var paid = paidBefore + payment.Amount;
            var closed = await BillCompletion.TryCompleteAsync(
                billStore,
                receiptIssuer,
                settlement.Bill,
                settlement.AcceptedOrders,
                settlement.Payments,
                actorId,
                now,
                cancellationToken);
            if (closed.IsFailure)
            {
                return Result.Failure<ConfirmPaymentResult>(closed.Error!);
            }

            return Result.Success(new ConfirmPaymentResult(
                payment.Id,
                payment.Status.ToString(),
                payment.Amount,
                payment.ClaimedAmount!.Value,
                paid,
                billTotal - paid,
                payment.ConfirmedAt!.Value));
        }

        private static ConfirmPaymentResult BuildResult(BillSettlement settlement, Payment payment, decimal billTotal)
        {
            var paid = Payment.ResolvePaidAmount(settlement.Payments);
            return new ConfirmPaymentResult(
                payment.Id,
                payment.Status.ToString(),
                payment.Amount,
                payment.ClaimedAmount!.Value,
                paid,
                billTotal - paid,
                payment.ConfirmedAt!.Value);
        }
    }
}
