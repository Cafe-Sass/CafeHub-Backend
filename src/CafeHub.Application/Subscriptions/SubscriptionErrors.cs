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

        public static Error RenewalRequestNotFound() =>
            Error.NotFound(
                "Subscription.RenewalRequestNotFound", "The renewal request was not found.");

        public static Error RenewalNotAllowed() =>
            Error.Conflict(
                "Subscription.RenewalNotAllowed", "The subscription cannot be renewed.");

        public static Error RenewalAlreadyOpen() =>
            Error.Conflict(
                "Subscription.RenewalAlreadyOpen", "The subscription already has an open renewal request.");

        public static Error RenewalNotApprovable() =>
            Error.Conflict(
                "Subscription.RenewalNotApprovable", "The renewal request cannot be changed in its current state.");

        public static Error RenewalExpired() =>
            Error.Conflict(
                "Subscription.RenewalExpired", "The renewal request has expired.");

        public static Error RenewalChargeFailed() =>
            Error.Conflict(
                "Subscription.RenewalChargeFailed", "The renewal charge did not succeed.");

        public static Error PlanVersionNotAvailable() =>
            Error.NotFound(
                "Subscription.PlanVersionNotAvailable", "The requested plan version is not available.");
    }
}
