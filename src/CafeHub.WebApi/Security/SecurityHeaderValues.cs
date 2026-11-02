namespace CafeHub.WebApi.Security
{
    /// <summary>
    /// Security response header names and values applied by <see cref="SecurityHeadersMiddleware"/>. All
    /// values live in one place so they are consistent across responses.
    /// </summary>
    public static class SecurityHeaderValues
    {
        public const string XContentTypeOptionsName = "X-Content-Type-Options";
        public const string XContentTypeOptionsValue = "nosniff";

        public const string XFrameOptionsName = "X-Frame-Options";
        public const string XFrameOptionsValue = "DENY";

        public const string ReferrerPolicyName = "Referrer-Policy";
        public const string ReferrerPolicyValue = "no-referrer";

        public const string PermissionsPolicyName = "Permissions-Policy";
        public const string PermissionsPolicyValue = "camera=(), microphone=(), geolocation=(), payment=()";

        public const string ContentSecurityPolicyName = "Content-Security-Policy";
        public const string ContentSecurityPolicyValue = "default-src 'none'; frame-ancestors 'none'";

        public const string CacheControlName = "Cache-Control";
        public const string CacheControlValue = "no-store";
    }
}
