using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Printing
{
    public static class PrintingErrors
    {
        public const string NotFoundCode = "Printers.NotFound";
        public const string BranchRequiredCode = "Printers.BranchRequired";
        public const string ConcurrencyConflictCode = "Printers.ConcurrencyConflict";
        public const string PrinterInactiveCode = "Printers.PrinterInactive";
        public const string InvalidStationCode = "Printers.InvalidStation";
        public const string RouteConflictCode = "Printers.RouteConflict";

        public static Error NotFound() =>
            Error.NotFound(NotFoundCode, "The printer or branch was not found.");

        public static Error BranchRequired() =>
            Error.Validation(BranchRequiredCode, "The branch id is required.");

        public static Error ConcurrencyConflict() =>
            Error.Conflict(
                ConcurrencyConflictCode,
                "The printer was modified by another request. Please reload and try again.");

        public static Error PrinterInactive() =>
            Error.Conflict(PrinterInactiveCode, "Only an active printer can be routed.");

        public static Error InvalidStation() =>
            Error.Validation(InvalidStationCode, "The station must belong to the same branch.");

        public static Error RouteConflict() =>
            Error.Conflict(RouteConflictCode, "The print route was changed concurrently. Please retry.");
    }
}
