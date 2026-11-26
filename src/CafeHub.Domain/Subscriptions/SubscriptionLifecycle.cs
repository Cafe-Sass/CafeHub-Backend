namespace CafeHub.Domain.Subscriptions
{
    public static class SubscriptionLifecycle
    {
        private static readonly IReadOnlyDictionary<SubscriptionStatus, IReadOnlySet<SubscriptionStatus>>
            AllowedTransitions = new Dictionary<SubscriptionStatus, IReadOnlySet<SubscriptionStatus>>
            {
                [SubscriptionStatus.Active] = new HashSet<SubscriptionStatus>
                {
                    SubscriptionStatus.Suspended,
                    SubscriptionStatus.Expired,
                    SubscriptionStatus.Cancelled,
                },
                [SubscriptionStatus.Suspended] = new HashSet<SubscriptionStatus>
                {
                    SubscriptionStatus.Active,
                    SubscriptionStatus.Expired,
                    SubscriptionStatus.Cancelled,
                },
                [SubscriptionStatus.Expired] = new HashSet<SubscriptionStatus>
                {
                    SubscriptionStatus.Active,
                    SubscriptionStatus.Cancelled,
                },
                [SubscriptionStatus.Cancelled] = new HashSet<SubscriptionStatus>(),
            };

        public static bool IsAllowed(SubscriptionStatus from, SubscriptionStatus to)
        {
            if (!Enum.IsDefined(from) || !Enum.IsDefined(to))
            {
                return false;
            }

            return AllowedTransitions[from].Contains(to);
        }

        public static IReadOnlySet<SubscriptionStatus> AllowedTargets(SubscriptionStatus from)
        {
            if (!Enum.IsDefined(from))
            {
                return new HashSet<SubscriptionStatus>();
            }

            return AllowedTransitions[from];
        }
    }
}
