using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Stations
{
    /// <summary>
    /// Error codes and messages for the stations module, following the <c>&lt;Module&gt;.&lt;Reason&gt;</c>
    /// convention with module <c>Stations</c>. Missing or out-of-scope stations and branches use the same
    /// not-found response; messages never echo an identifier.
    /// </summary>
    public static class StationsErrors
    {
        public const string NotFoundCode = "Stations.NotFound";
        public const string NotFoundMessage = "The station was not found.";

        public static Error NotFound()
        {
            return Error.NotFound(NotFoundCode, NotFoundMessage);
        }

        public const string BranchNotFoundCode = "Stations.BranchNotFound";
        public const string BranchNotFoundMessage = "The branch was not found.";

        public static Error BranchNotFound()
        {
            return Error.NotFound(BranchNotFoundCode, BranchNotFoundMessage);
        }

        public const string BranchNotActiveCode = "Stations.BranchNotActive";
        public const string BranchNotActiveMessage = "The branch is not active.";

        public static Error BranchNotActive()
        {
            return Error.Conflict(BranchNotActiveCode, BranchNotActiveMessage);
        }

        public const string NameConflictCode = "Stations.NameConflict";
        public const string NameConflictMessage = "An active station with this name already exists in the branch.";

        public static Error NameConflict()
        {
            return Error.Conflict(NameConflictCode, NameConflictMessage);
        }

        public const string ConcurrencyConflictCode = "Stations.ConcurrencyConflict";
        public const string ConcurrencyConflictMessage = "The station was modified by another request. Please reload and try again.";

        public static Error ConcurrencyConflict()
        {
            return Error.Conflict(ConcurrencyConflictCode, ConcurrencyConflictMessage);
        }

        public const string InvalidStatusCode = "Stations.InvalidStatus";
        public const string InvalidStatusMessage = "The status is not valid.";

        public static ValidationError InvalidStatus()
        {
            return ValidationError.Create(new[] { new FieldError("status", InvalidStatusMessage) });
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
