using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Pagination;
using CafeHub.Application.Plans.Contracts;
using CafeHub.Application.Plans.DTOs;

namespace CafeHub.Application.Plans.Queries
{
    public sealed class ListPlansQueryHandler : IQueryHandler<ListPlansQuery, OffsetPage<PlanResponse>>
    {
        private readonly IPlanRepository planRepository;
        private readonly PaginationOptions paginationOptions;

        public ListPlansQueryHandler(IPlanRepository planRepository, PaginationOptions paginationOptions)
        {
            this.planRepository = planRepository ?? throw new ArgumentNullException(nameof(planRepository));
            this.paginationOptions = paginationOptions ?? throw new ArgumentNullException(nameof(paginationOptions));
        }

        public async Task<Result<OffsetPage<PlanResponse>>> Handle(ListPlansQuery query, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var pageSize = paginationOptions.ResolvePageSize(query.PageSize);
            var requestedSort = query.Sort is null
                ? null
                : new[] { new SortRequest(query.Sort, query.SortDirection) };
            var resolvedSort = PlanListSort.Whitelist.Resolve(requestedSort);
            if (resolvedSort.IsFailure)
            {
                return Result.Failure<OffsetPage<PlanResponse>>(resolvedSort.Error!);
            }

            var listPage = await planRepository.ListAsync(
                new PlanListRequest(query.Page, pageSize, resolvedSort.Value, query.IncludeTotalCount),
                cancellationToken);

            var page = OffsetPage<PlanResponse>.Create(
                listPage.FetchedItems.Select(PlanResponse.From).ToArray(),
                query.Page,
                pageSize,
                listPage.TotalCount);

            return Result.Success(page);
        }
    }
}
