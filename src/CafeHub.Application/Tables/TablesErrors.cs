using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Tables
{
    /// <summary>
    /// Error codes and messages for the tables module, following the <c>&lt;Module&gt;.&lt;Reason&gt;</c>
    /// convention with module <c>Tables</c>. Missing or out-of-scope tables and branches use the same
    /// not-found response; messages never echo an identifier.
    /// </summary>
    public static class TablesErrors
    {
        public const string NotFoundCode = "Tables.NotFound";
        public const string NotFoundMessage = "The table was not found.";

        public static Error NotFound()
        {
            return Error.NotFound(NotFoundCode, NotFoundMessage);
        }

        public const string BranchNotFoundCode = "Tables.BranchNotFound";
        public const string BranchNotFoundMessage = "The branch was not found.";

        public static Error BranchNotFound()
        {
            return Error.NotFound(BranchNotFoundCode, BranchNotFoundMessage);
        }

        public const string BranchNotActiveCode = "Tables.BranchNotActive";
        public const string BranchNotActiveMessage = "The branch is not active.";

        public static Error BranchNotActive()
        {
            return Error.Conflict(BranchNotActiveCode, BranchNotActiveMessage);
        }

        public const string NumberConflictCode = "Tables.NumberConflict";
        public const string NumberConflictMessage = "An active table with this number already exists in the branch.";

        public static Error NumberConflict()
        {
            return Error.Conflict(NumberConflictCode, NumberConflictMessage);
        }

        public const string NumberRangeExceededCode = "Tables.NumberRangeExceeded";
        public const string NumberRangeExceededMessage = "The table number cannot exceed 9999.";

        public static ValidationError NumberRangeExceeded()
        {
            return ValidationError.Create(new[] { new FieldError("number", NumberRangeExceededMessage) });
        }

        public const string BulkCountOutOfRangeCode = "Tables.BulkCountOutOfRange";
        public const string BulkCountOutOfRangeMessage = "The bulk count must be between 1 and 200.";

        public static ValidationError BulkCountOutOfRange()
        {
            return ValidationError.Create(new[] { new FieldError("count", BulkCountOutOfRangeMessage) });
        }

        public const string BulkCountUnavailableCode = "Tables.BulkCountUnavailable";
        public const string BulkCountUnavailableMessage = "There are not enough active tables to deactivate.";

        public static ValidationError BulkCountUnavailable()
        {
            return ValidationError.Create(new[] { new FieldError("count", BulkCountUnavailableMessage) });
        }

        public const string ConcurrencyConflictCode = "Tables.ConcurrencyConflict";
        public const string ConcurrencyConflictMessage = "The table was modified by another request. Please reload and try again.";

        public static Error ConcurrencyConflict()
        {
            return Error.Conflict(ConcurrencyConflictCode, ConcurrencyConflictMessage);
        }

        public const string QrCodeConflictCode = "Tables.QrCodeConflict";
        public const string QrCodeConflictMessage = "The table QR code is already in use. Please try again.";

        public static Error QrCodeConflict()
        {
            return Error.Conflict(QrCodeConflictCode, QrCodeConflictMessage);
        }

        public static ValidationError InvalidConcurrencyToken()
        {
            return ValidationError.Create(new[]
            {
                new FieldError("concurrencyToken", "The concurrency token is not a valid unsigned integer."),
            });
        }
    }
}
