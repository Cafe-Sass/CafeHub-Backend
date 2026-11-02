using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.CafeProfile
{
    /// <summary>
    /// Error codes and messages for the cafe profile module, following the <c>&lt;Module&gt;.&lt;Reason&gt;</c>
    /// convention with module <c>CafeProfile</c>. A missing settings row maps to NotFound; the message never
    /// names or echoes a tenant or internal identifier.
    /// </summary>
    public static class CafeProfileErrors
    {
        public const string NotFoundCode = "CafeProfile.NotFound";
        public const string NotFoundMessage = "The cafe profile was not found.";

        public static Error NotFound()
        {
            return Error.NotFound(NotFoundCode, NotFoundMessage);
        }
    }
}
