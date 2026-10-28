using CafeHub.Domain.Onboarding;

namespace CafeHub.Application.Onboarding
{
    public sealed record OwnerRegistrationResponse(
        Guid RequestId,
        Guid PlanVersionId,
        OnboardingRequestStatus State,
        bool IsDuplicate);
}
