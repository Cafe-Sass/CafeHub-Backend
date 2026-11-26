namespace CafeHub.Application.Subscriptions
{
    public static class SubscriptionConfigurationKeys
    {
        public const string SectionName = "Subscription";
        public const string GraceDaysKey = "GraceDays";
        public const string SuspendedRetentionDaysKey = "SuspendedRetentionDays";
        public const string RenewalRequestLifetimeDaysKey = "RenewalRequestLifetimeDays";

        public const string GraceDaysPath = $"{SectionName}:{GraceDaysKey}";
        public const string SuspendedRetentionDaysPath = $"{SectionName}:{SuspendedRetentionDaysKey}";
        public const string RenewalRequestLifetimeDaysPath =
            $"{SectionName}:{RenewalRequestLifetimeDaysKey}";

        public const int DefaultGraceDays = 3;
        public const int DefaultSuspendedRetentionDays = 90;
        public const int DefaultRenewalRequestLifetimeDays = 7;
    }
}
