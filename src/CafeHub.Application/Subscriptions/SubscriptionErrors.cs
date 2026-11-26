using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Subscriptions
{
    public static class SubscriptionErrors
    {
        public static Error NotFound() =>
            Error.NotFound("Subscription.NotFound", "The subscription was not found.");

        public static Error InvalidTransition() =>
            Error.Conflict(
                "Subscription.InvalidTransition",
                "The subscription cannot move to the requested state.");
    }
}
