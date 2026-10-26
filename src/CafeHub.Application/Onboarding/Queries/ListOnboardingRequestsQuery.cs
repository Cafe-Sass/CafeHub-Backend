using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Onboarding.DTOs;
using CafeHub.Application.Pagination;

namespace CafeHub.Application.Onboarding.Queries
{
    /// <summary>
    /// Lists onboarding requests ready for review. The queue defaults to
    /// <see cref="OnboardingRequestStatus.PaidAwaitingReview"/> and uses offset paging with an optional total
    /// count. A <c>state</c> query string overrides the default filter; an unknown state value fails validation.
    /// </summary>
    public sealed record ListOnboardingRequestsQuery : IQuery<OffsetPage<OnboardingRequestListItem>>
    {
        public int Page { get; init; } = 1;

        public int? PageSize { get; init; }

        public bool IncludeTotalCount { get; init; }

        public string? State { get; init; }

        public string? Sort { get; init; }

        public SortDirection SortDirection { get; init; } = SortDirection.Asc;
    }
}
