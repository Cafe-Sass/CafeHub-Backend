using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Refunds
{
    public static class RefundRequestErrors
    {
        public const string NotFoundCode = "RefundRequests.NotFound";
        public const string OpenRequestLimitReachedCode = "RefundRequests.OpenRequestLimitReached";
        public const string AmountAboveRefundableCode = "RefundRequests.AmountAboveRefundable";

        public static Error NotFound() =>
            Error.NotFound(NotFoundCode, "The refund request was not found.");

        public static Error OpenRequestLimitReached(int limit) =>
            Error.Conflict(OpenRequestLimitReachedCode, $"The bill already has {limit} open refund requests.");

        public static Error AmountAboveRefundable() =>
            Error.Conflict(AmountAboveRefundableCode, "The claimed amount exceeds the remaining refundable payment amount.");
    }
}
