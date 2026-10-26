using CafeHub.Application.Pagination;
using CafeHub.Domain.Onboarding;

namespace CafeHub.Application.Onboarding.Contracts
{
    /// <summary>
    /// Offset-page request for the onboarding review queue. <see cref="PageSize"/> is the resolved page size;
    /// the store fetches at most one extra row so the caller can derive <see cref="OffsetPage{T}.HasNextPage"/>
    /// without a count. <see cref="State"/> is the resolved onboarding status filter; the default queue filter
    /// is <see cref="OnboardingRequestStatus.PaidAwaitingReview"/> and is resolved by the handler before the
    /// request reaches the store.
    /// </summary>
    public sealed record OnboardingRequestListRequest(
        int Page,
        int PageSize,
        IReadOnlyList<SortTerm> Sort,
        OnboardingRequestStatus? State,
        bool IncludeTotalCount);
}
