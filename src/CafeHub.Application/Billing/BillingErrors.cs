using CafeHub.Application.Abstractions.Results;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Billing
{
    public static class BillingErrors
    {
        public const string NotFoundCode = "Billing.NotFound";
        public const string BranchRequiredCode = "Billing.BranchRequired";
        public const string BillClosedCode = "Billing.BillClosed";
        public const string OrderNotOnBillCode = "Billing.OrderNotOnBill";
        public const string AmountAboveRemainingCode = "Billing.AmountAboveRemaining";
        public const string PaymentNotFoundCode = "Billing.PaymentNotFound";
        public const string PaymentNotPendingReviewCode = "Billing.PaymentNotPendingReview";
        public const string PendingProofCode = "Billing.PendingProof";

        public static Error NotFound() =>
            Error.NotFound(NotFoundCode, "The bill was not found.");

        public static Error BranchRequired() =>
            Error.Validation(BranchRequiredCode, "The branch id is required.");

        public static Error BillClosed() =>
            Error.Conflict(BillClosedCode, "The bill is already closed.");

        public static Error OrderNotOnBill() =>
            Error.Conflict(OrderNotOnBillCode, "The linked order does not belong to this bill.");

        public static Error AmountAboveRemaining() =>
            Error.Conflict(AmountAboveRemainingCode, "The amount exceeds the remaining amount to settle.");

        public static Error PaymentNotFound() =>
            Error.NotFound(PaymentNotFoundCode, "The payment was not found.");

        public static Error PaymentNotPendingReview(PaymentStatus status) =>
            Error.Conflict(
                PaymentNotPendingReviewCode,
                $"Only a payment awaiting review can be decided. The payment status is {status}.");

        public static Error PendingProof() =>
            Error.Conflict(PendingProofCode, "The bill cannot close while a payment proof awaits review.");

        public const string PendingProofLimitReachedCode = "Billing.PendingProofLimitReached";

        public static Error PendingProofLimitReached(int limit) =>
            Error.Conflict(
                PendingProofLimitReachedCode,
                $"The bill already carries the maximum of {limit} payment proofs awaiting review.");
    }
}
