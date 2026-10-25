using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Identity
{
    /// <summary>
    /// Error codes and messages for the admin authentication module, following the <c>&lt;Module&gt;.&lt;Reason&gt;</c>
    /// convention with module <c>Auth</c>. Credential rejections map to the single safe
    /// <c>Auth.InvalidCredentials</c> Unauthorized response, refresh-session rejections map to the single
    /// safe <c>Auth.InvalidRefreshToken</c> Unauthorized response, and a principal whose administrator is
    /// missing or inactive maps to the single safe <c>Auth.CurrentAdminUnavailable</c> Unauthorized response;
    /// the messages never name or echo a password, a refresh token, an email value, or an administrator id.
    /// </summary>
    public static class AuthErrors
    {
        public const string InvalidCredentialsCode = "Auth.InvalidCredentials";
        public const string InvalidCredentialsMessage = "The credentials are not valid.";
        public const string InvalidRefreshTokenCode = "Auth.InvalidRefreshToken";
        public const string InvalidRefreshTokenMessage = "The refresh token is not valid.";
        public const string CurrentAdminUnavailableCode = "Auth.CurrentAdminUnavailable";
        public const string CurrentAdminUnavailableMessage = "The current administrator is not available.";

        public static Error InvalidCredentials()
        {
            return Error.Unauthorized(InvalidCredentialsCode, InvalidCredentialsMessage);
        }

        public static Error InvalidRefreshToken()
        {
            return Error.Unauthorized(InvalidRefreshTokenCode, InvalidRefreshTokenMessage);
        }

        public static Error CurrentAdminUnavailable()
        {
            return Error.Unauthorized(CurrentAdminUnavailableCode, CurrentAdminUnavailableMessage);
        }
    }
}
