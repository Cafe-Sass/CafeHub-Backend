using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Billing
{
    public static class PaymentSettingsErrors
    {
        public const string NotFoundCode = "PaymentSettings.NotFound";
        public const string ConcurrencyConflictCode = "PaymentSettings.ConcurrencyConflict";

        public static Error NotFound() =>
            Error.NotFound(NotFoundCode, "The payment settings were not found.");

        public static Error ConcurrencyConflict() =>
            Error.Conflict(
                ConcurrencyConflictCode,
                "The payment settings were modified by another request. Please reload and try again.");
    }
}
