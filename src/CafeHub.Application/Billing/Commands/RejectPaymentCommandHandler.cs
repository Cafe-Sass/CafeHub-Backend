using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Billing.Contracts;
using CafeHub.Application.Billing.DTOs;
using CafeHub.Application.Identity;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Billing.Commands
{
    public sealed class RejectPaymentCommandHandler(
        IPaymentStore store,
        IBillStore billStore,
        IReceiptIssuer receiptIssuer,
        ICurrentTenantUser currentUser,
        TimeProvider timeProvider)
        : ICommandHandler<RejectPaymentCommand, RejectPaymentResult>
    {
        public async Task<Result<RejectPaymentResult>> Handle(
            RejectPaymentCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<RejectPaymentResult>(scope.Error!);
            }

            if (currentUser.UserId is not { } actorId || actorId == Guid.Empty)
            {
                return Result.Failure<RejectPaymentResult>(AuthErrors.Forbidden());
            }

            var review = await store.FindReviewAsync(scope.Value, command.PaymentId, cancellationToken);
            if (review is null
                || review.Payment.Method is not PaymentMethod.Online
                || review.Payment.ClaimedAmount is null)
            {
                return Result.Failure<RejectPaymentResult>(BillingErrors.PaymentNotFound());
            }

            var payment = review.Payment;
            var settlement = review.Settlement;
            var billTotal = Bill.ResolveTotalAmount(settlement.AcceptedOrders);
            var paid = Payment.ResolvePaidAmount(settlement.Payments);
            if (payment.Status is PaymentStatus.Confirmed)
            {
                return Result.Failure<RejectPaymentResult>(
                    BillingErrors.PaymentNotPendingReview(payment.Status));
            }

            if (payment.Status is PaymentStatus.PendingReview)
            {
                var now = timeProvider.GetUtcNow();
                var rejectionReason = command.Reason!.Trim();
                payment.RejectReview(rejectionReason, actorId, now);
                var saved = await store.SaveReviewAsync(payment, rejectionReason, cancellationToken);
                if (saved.IsFailure)
                {
                    return Result.Failure<RejectPaymentResult>(saved.Error!);
                }

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
                    return Result.Failure<RejectPaymentResult>(closed.Error!);
                }
            }

            return Result.Success(new RejectPaymentResult(
                payment.Id,
                payment.Status.ToString(),
                paid,
                billTotal - paid));
        }
    }
}
