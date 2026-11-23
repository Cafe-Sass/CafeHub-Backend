using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Billing
{
    public static class BillingErrors
    {
        public const string NotFoundCode = "Billing.NotFound";
        public const string BillClosedCode = "Billing.BillClosed";
        public const string OrderNotOnBillCode = "Billing.OrderNotOnBill";
        public const string AmountAboveRemainingCode = "Billing.AmountAboveRemaining";

        public static Error NotFound() =>
            Error.NotFound(NotFoundCode, "The bill was not found.");

        public static Error BillClosed() =>
            Error.Conflict(BillClosedCode, "The bill is already closed.");

        public static Error OrderNotOnBill() =>
            Error.Conflict(OrderNotOnBillCode, "The linked order does not belong to this bill.");

        public static Error AmountAboveRemaining() =>
            Error.Conflict(AmountAboveRemainingCode, "The amount exceeds the remaining amount to settle.");
    }
}
