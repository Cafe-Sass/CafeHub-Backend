namespace CafeHub.Application.Subscriptions
{
    public sealed class SubscriptionOptions
    {
        public int GraceDays { get; set; } = SubscriptionConfigurationKeys.DefaultGraceDays;

        public int SuspendedRetentionDays { get; set; } =
            SubscriptionConfigurationKeys.DefaultSuspendedRetentionDays;

        public IReadOnlyList<string> Validate()
        {
            var problems = new List<string>();
            if (GraceDays < 0)
            {
                problems.Add(
                    $"{SubscriptionConfigurationKeys.GraceDaysPath} must be zero or greater but was {GraceDays}.");
            }

            if (SuspendedRetentionDays < 0)
            {
                problems.Add(
                    $"{SubscriptionConfigurationKeys.SuspendedRetentionDaysPath} must be zero or greater but was {SuspendedRetentionDays}.");
            }

            return problems;
        }
    }
}
