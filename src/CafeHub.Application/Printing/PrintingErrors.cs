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
        public const string JobNotFoundCode = "Printers.JobNotFound";
        public const string JobNotReportableCode = "Printers.JobNotReportable";
        public const string JobNotCancellableCode = "Printers.JobNotCancellable";
        public const string JobNotRequeuableCode = "Printers.JobNotRequeuable";
        public const string InvalidFailureCodeCode = "Printers.InvalidFailureCode";
        public const string InvalidJobFilterCode = "Printers.InvalidJobFilter";
        public const string ConnectorNotFoundCode = "Printers.ConnectorNotFound";
        public const string ConnectorUnauthorizedCode = "Printers.ConnectorUnauthorized";

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

        public static Error JobNotFound() =>
            Error.NotFound(JobNotFoundCode, "The print job was not found.");

        public static Error JobNotReportable() =>
            Error.Conflict(JobNotReportableCode, "The print job is not claimed by a connector.");

        public static Error JobNotCancellable() =>
            Error.Conflict(JobNotCancellableCode, "A printed print job cannot be cancelled.");

        public static Error JobNotRequeuable() =>
            Error.Conflict(JobNotRequeuableCode, "Only a failed print job can be requeued.");

        public static Error InvalidFailureCode() =>
            Error.Validation(
                InvalidFailureCodeCode,
                "The failure code must be PrinterOffline, PaperOut, PayloadRejected, or Unknown.");

        public static Error InvalidJobFilter(string field) =>
            Error.Validation(InvalidJobFilterCode, $"The {field} filter is not valid.");

        public static Error ConnectorNotFound() =>
            Error.NotFound(ConnectorNotFoundCode, "The print connector was not found.");

        public static Error ConnectorUnauthorized() =>
            Error.Unauthorized(ConnectorUnauthorizedCode, "The connector key is not valid.");
    }
}
