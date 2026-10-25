namespace CafeHub.WebApi.RateLimiting
{
    public static class AuthRateLimitConfigurationKeys
    {
        public const string SectionName = "RateLimiting:AdminAuth";
        public const string PermitLimitKey = "PermitLimit";
        public const string WindowSecondsKey = "WindowSeconds";

        public const string PermitLimitPath = $"{SectionName}:{PermitLimitKey}";
        public const string WindowSecondsPath = $"{SectionName}:{WindowSecondsKey}";
    }
}
