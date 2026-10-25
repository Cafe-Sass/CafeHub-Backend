namespace CafeHub.WebApi.RateLimiting
{
    /// <summary>
    /// Constants shared by the admin authentication rate-limiting policy, its endpoint metadata, and the
    /// ProblemDetails body written when the policy rejects a request. The rejection follows the
    /// <c>&lt;Module&gt;.&lt;Reason&gt;</c> error-code convention with module <c>Auth</c>.
    /// </summary>
    public static class AuthRateLimiting
    {
        public const string PolicyName = "AdminAuth";

        public const string ErrorCode = "Auth.RateLimitExceeded";
        public const string ErrorDetail = "Too many authentication requests were received. Try again later.";

        public const string ProblemType = "https://tools.ietf.org/html/rfc6585#section-4";
        public const string ProblemTitle = "Too Many Requests";
    }
}
