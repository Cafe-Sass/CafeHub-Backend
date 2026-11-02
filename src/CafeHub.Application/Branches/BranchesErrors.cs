using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Branches
{
    /// <summary>
    /// Error codes and messages for the branches module, following the <c>&lt;Module&gt;.&lt;Reason&gt;</c>
    /// convention with module <c>Branches</c>. A missing branch maps to NotFound and a stale concurrency
    /// token maps to Conflict; the messages never name or echo a branch identifier.
    /// </summary>
    public static class BranchesErrors
    {
        public const string NotFoundCode = "Branches.NotFound";
        public const string NotFoundMessage = "The branch was not found.";

        public static Error NotFound()
        {
            return Error.NotFound(NotFoundCode, NotFoundMessage);
        }

        public const string ConcurrencyConflictCode = "Branches.ConcurrencyConflict";
        public const string ConcurrencyConflictMessage = "The branch was modified by another request. Please reload and try again.";

        public static Error ConcurrencyConflict()
        {
            return Error.Conflict(ConcurrencyConflictCode, ConcurrencyConflictMessage);
        }

        public const string InvalidStatusCode = "Branches.InvalidStatus";
        public const string InvalidStatusMessage = "The status is not valid.";

        public static ValidationError InvalidStatus()
        {
            return ValidationError.Create(new[] { new FieldError("status", InvalidStatusMessage) });
        }

        public const string IllegalTransitionCode = "Branches.IllegalTransition";
        public const string IllegalTransitionMessage = "The requested lifecycle transition is not allowed.";

        public static Error IllegalTransition()
        {
            return Error.Conflict(IllegalTransitionCode, IllegalTransitionMessage);
        }
    }
}
