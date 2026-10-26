using CafeHub.Domain.Onboarding;

namespace CafeHub.Application.Onboarding.Contracts
{
    /// <summary>
    /// Offset page result returned by the onboarding review queue store. <see cref="TotalCount"/> is populated
    /// only when the request asked for it; otherwise it is null.
    /// </summary>
    public sealed record OnboardingRequestListPage(IReadOnlyList<OnboardingRequest> FetchedItems, int? TotalCount);
}
