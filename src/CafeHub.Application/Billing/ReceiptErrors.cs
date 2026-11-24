using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Billing
{
    public static class ReceiptErrors
    {
        public const string NotFoundCode = "Receipts.NotFound";

        public static Error NotFound() =>
            Error.NotFound(NotFoundCode, "The receipt was not found.");
    }
}
