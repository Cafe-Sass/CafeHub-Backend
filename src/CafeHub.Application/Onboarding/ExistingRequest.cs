using CafeHub.Domain.Onboarding;

namespace CafeHub.Application.Onboarding
{
    /// <summary>
    /// The duplicate public-registration outcome: the email already has a request, so nothing is created and the
    /// existing request's reference and state are returned instead. This is not an error (FR-ONB-004).
    /// </summary>
    public sealed record ExistingRequest(Guid RequestId, OnboardingRequestStatus State);
}
