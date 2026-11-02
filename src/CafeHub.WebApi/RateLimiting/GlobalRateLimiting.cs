namespace CafeHub.WebApi.RateLimiting
{
    /// <summary>
    /// Constants shared by the global rate-limiting policy and the ProblemDetails body written when the
    /// policy rejects a request. The rejection follows the <c>&lt;Module&gt;.&lt;Reason&gt;</c> error-code
    /// convention with module <c>Api</c>.
    /// </summary>
    public static class GlobalRateLimiting
    {
        public const string PolicyName = "Global";

        public const string ErrorCode = "Api.RateLimitExceeded";
        public const string ErrorDetail = "Too many requests were received. Try again later.";

        public const string ProblemType = "https://tools.ietf.org/html/rfc6585#section-4";
        public const string ProblemTitle = "Too Many Requests";
    }
}
