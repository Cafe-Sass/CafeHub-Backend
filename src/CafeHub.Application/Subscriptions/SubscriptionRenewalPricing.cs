using CafeHub.Domain.Plans;

namespace CafeHub.Application.Subscriptions
{
    public static class SubscriptionRenewalPricing
    {
        public static DateTimeOffset NextEnd(DateTimeOffset startsAt, BillingPeriod billingPeriod) =>
            billingPeriod switch
            {
                BillingPeriod.Monthly => startsAt.AddMonths(1),
                BillingPeriod.Annual => startsAt.AddYears(1),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(billingPeriod), "The billing period is not defined."),
            };
    }
}
