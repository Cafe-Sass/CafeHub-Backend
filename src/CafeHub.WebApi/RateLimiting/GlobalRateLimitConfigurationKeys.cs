namespace CafeHub.WebApi.RateLimiting
{
    public static class GlobalRateLimitConfigurationKeys
    {
        public const string SectionName = "RateLimiting:Global";
        public const string PermitLimitKey = "PermitLimit";
        public const string WindowSecondsKey = "WindowSeconds";

        public const string PermitLimitPath = $"{SectionName}:{PermitLimitKey}";
        public const string WindowSecondsPath = $"{SectionName}:{WindowSecondsKey}";
    }
}
