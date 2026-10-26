using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Onboarding.Contracts;
using CafeHub.Application.Onboarding.DTOs;
using CafeHub.Application.Pagination;
using CafeHub.Domain.Onboarding;

namespace CafeHub.Application.Onboarding.Queries
{
    public sealed class ListOnboardingRequestsQueryHandler : IQueryHandler<ListOnboardingRequestsQuery, OffsetPage<OnboardingRequestListItem>>
    {
        private readonly IOnboardingRequestStore onboardingRequestStore;
        private readonly PaginationOptions paginationOptions;

        public ListOnboardingRequestsQueryHandler(
            IOnboardingRequestStore onboardingRequestStore,
            PaginationOptions paginationOptions)
        {
            this.onboardingRequestStore = onboardingRequestStore ?? throw new ArgumentNullException(nameof(onboardingRequestStore));
            this.paginationOptions = paginationOptions ?? throw new ArgumentNullException(nameof(paginationOptions));
        }

        public async Task<Result<OffsetPage<OnboardingRequestListItem>>> Handle(ListOnboardingRequestsQuery query, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var pageSize = paginationOptions.ResolvePageSize(query.PageSize);

            var requestedSort = query.Sort is null
                ? null
                : new[] { new SortRequest(query.Sort, query.SortDirection) };
            var resolvedSort = OnboardingRequestListSort.Whitelist.Resolve(requestedSort);
            if (resolvedSort.IsFailure)
            {
                return Result.Failure<OffsetPage<OnboardingRequestListItem>>(resolvedSort.Error!);
            }

            var state = OnboardingRequestStatus.PaidAwaitingReview;
            if (query.State is not null)
            {
                state = Enum.Parse<OnboardingRequestStatus>(query.State, ignoreCase: true);
            }

            var listPage = await onboardingRequestStore.ListForReviewAsync(
                new OnboardingRequestListRequest(query.Page, pageSize, resolvedSort.Value, state, query.IncludeTotalCount),
                cancellationToken);

            var page = OffsetPage<OnboardingRequestListItem>.Create(
                listPage.FetchedItems.Select(OnboardingRequestListItem.From).ToArray(),
                query.Page,
                pageSize,
                listPage.TotalCount);

            return Result.Success(page);
        }
    }
}
