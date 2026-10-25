using CafeHub.Domain.Onboarding;
using CafeHub.Domain.Subscriptions;

namespace CafeHub.Application.Onboarding.PaymentGateway
{
    public sealed record PaymentOutcomeResponse(Guid RequestId, OnboardingRequestStatus State, PlatformPaymentTransactionStatus PaymentStatus);
}
