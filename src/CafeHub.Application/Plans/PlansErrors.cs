using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Plans
{
    /// <summary>
    /// Error codes and messages for the plans module, following the <c>&lt;Module&gt;.&lt;Reason&gt;</c>
    /// convention with module <c>Plans</c>. A duplicate stable key maps to Conflict and an unknown plan maps to
    /// NotFound; the messages never echo a key or an internal identifier.
    /// </summary>
    public static class PlansErrors
    {
        public const string KeyAlreadyExistsCode = "Plans.KeyAlreadyExists";
        public const string KeyAlreadyExistsMessage = "A plan with the same key already exists.";
        public const string NotFoundCode = "Plans.NotFound";
        public const string NotFoundMessage = "The plan was not found.";

        public static Error KeyAlreadyExists()
        {
            return Error.Conflict(KeyAlreadyExistsCode, KeyAlreadyExistsMessage);
        }

        public static Error NotFound()
        {
            return Error.NotFound(NotFoundCode, NotFoundMessage);
        }
    }
}
