namespace CafeHub.Infrastructure.Authentication
{
    public static class RefreshTokenConfigurationKeys
    {
        public const string SectionName = "RefreshTokens";
        public const string LifetimeDaysKey = "LifetimeDays";

        public const string LifetimeDaysPath = $"{SectionName}:{LifetimeDaysKey}";
    }
}
