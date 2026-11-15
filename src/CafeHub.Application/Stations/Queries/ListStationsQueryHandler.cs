using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Pagination;
using CafeHub.Application.Stations.Contracts;
using CafeHub.Application.Stations.DTOs;

namespace CafeHub.Application.Stations.Queries
{
    public sealed class ListStationsQueryHandler : IQueryHandler<ListStationsQuery, OffsetPage<StationResponse>>
    {
        private readonly IStationStore stationStore;
        private readonly ICurrentTenantUser currentUser;
        private readonly PaginationOptions paginationOptions;

        public ListStationsQueryHandler(
            IStationStore stationStore,
            ICurrentTenantUser currentUser,
            PaginationOptions paginationOptions)
        {
            this.stationStore = stationStore ?? throw new ArgumentNullException(nameof(stationStore));
            this.currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            this.paginationOptions = paginationOptions ?? throw new ArgumentNullException(nameof(paginationOptions));
        }

        public async Task<Result<OffsetPage<StationResponse>>> Handle(
            ListStationsQuery query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<OffsetPage<StationResponse>>(scope.Error!);
            }

            if (scope.Value is { } branchId && branchId != query.BranchId)
            {
                return Result.Failure<OffsetPage<StationResponse>>(StationsErrors.NotFound());
            }

            var pageSize = paginationOptions.ResolvePageSize(query.PageSize);
            var listPage = await stationStore.ListAsync(
                new StationListRequest(query.BranchId, query.Page, pageSize, query.IncludeTotalCount, query.Status),
                cancellationToken);

            var page = OffsetPage<StationResponse>.Create(
                listPage.FetchedItems,
                query.Page,
                pageSize,
                listPage.TotalCount);

            return Result.Success(page);
        }
    }
}
