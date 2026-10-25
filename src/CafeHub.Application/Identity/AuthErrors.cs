using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Identity
{
    /// <summary>
    /// Error codes and messages for the admin authentication module, following the <c>&lt;Module&gt;.&lt;Reason&gt;</c>
    /// convention with module <c>Auth</c>. Every rejection maps to the single safe <c>Auth.InvalidCredentials</c>
    /// Unauthorized response; the messages never name or echo a password, a refresh token, or an email value.
    /// </summary>
    public static class AuthErrors
    {
        public const string InvalidCredentialsCode = "Auth.InvalidCredentials";
        public const string InvalidCredentialsMessage = "The credentials are not valid.";

        public static Error InvalidCredentials()
        {
            return Error.Unauthorized(InvalidCredentialsCode, InvalidCredentialsMessage);
        }
    }
}
