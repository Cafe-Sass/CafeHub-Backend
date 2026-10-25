namespace CafeHub.Domain.Onboarding
{
    public enum OnboardingRequestStatus
    {
        AwaitingPayment,
        PaymentFailed,
        PaidAwaitingReview,
        Approved,
        Rejected,
        Cancelled,
    }
}
