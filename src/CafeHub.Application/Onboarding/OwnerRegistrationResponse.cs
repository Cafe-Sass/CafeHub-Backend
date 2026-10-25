using CafeHub.Domain.Onboarding;

namespace CafeHub.Application.Onboarding
{
    public sealed record OwnerRegistrationResponse(Guid RequestId, OnboardingRequestStatus State);
}
