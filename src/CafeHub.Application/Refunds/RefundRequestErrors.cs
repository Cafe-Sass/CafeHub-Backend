using CafeHub.Application.Abstractions.Results;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Refunds
{
    public static class RefundRequestErrors
    {
        public const string NotFoundCode = "RefundRequests.NotFound";
        public const string OpenRequestLimitReachedCode = "RefundRequests.OpenRequestLimitReached";
        public const string AmountAboveRefundableCode = "RefundRequests.AmountAboveRefundable";
        public const string StatusConflictCode = "RefundRequests.StatusConflict";
        public const string ConcurrencyConflictCode = "RefundRequests.ConcurrencyConflict";
        public const string AssignedStaffNotFoundCode = "RefundRequests.AssignedStaffNotFound";

        public static Error NotFound() =>
            Error.NotFound(NotFoundCode, "The refund request was not found.");

        public static Error OpenRequestLimitReached(int limit) =>
            Error.Conflict(OpenRequestLimitReachedCode, $"The bill already has {limit} open refund requests.");

        public static Error AmountAboveRefundable() =>
            Error.Conflict(AmountAboveRefundableCode, "The claimed amount exceeds the remaining refundable payment amount.");

        public static Error StatusConflict(RefundRequestStatus status) =>
            Error.Conflict(StatusConflictCode, $"The refund request cannot change from its current status {status}.");

        public static Error ConcurrencyConflict() =>
            Error.Conflict(ConcurrencyConflictCode, "The refund request changed concurrently. Reload it and retry.");

        public static Error AssignedStaffNotFound() =>
            Error.Validation(
                AssignedStaffNotFoundCode,
                "The assigned user must be an active staff member of the request branch or the owner.");
    }
}
