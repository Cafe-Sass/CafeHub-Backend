using CafeHub.Domain.Subscriptions;

namespace CafeHub.Application.Subscriptions
{
    public static class SubscriptionLifecyclePolicy
    {
        public static SubscriptionStatus NextState(
            SubscriptionStatus current,
            DateTimeOffset? periodEndsAt,
            DateTimeOffset now,
            SubscriptionOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            if (!Enum.IsDefined(current))
            {
                throw new ArgumentOutOfRangeException(nameof(current), "The subscription status is not defined.");
            }

            if (periodEndsAt is not { } endsAt)
            {
                return current;
            }

            var graceEndsAt = endsAt.AddDays(options.GraceDays);
            var retentionEndsAt = graceEndsAt.AddDays(options.SuspendedRetentionDays);

            return current switch
            {
                SubscriptionStatus.Active when now >= graceEndsAt => SubscriptionStatus.Suspended,
                SubscriptionStatus.Suspended when now >= retentionEndsAt => SubscriptionStatus.Expired,
                _ => current,
            };
        }
    }
}
