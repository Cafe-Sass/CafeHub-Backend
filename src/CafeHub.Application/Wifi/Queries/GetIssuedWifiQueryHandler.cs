using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Pagination;
using CafeHub.Application.Wifi.Contracts;
using CafeHub.Application.Wifi.DTOs;

namespace CafeHub.Application.Wifi.Queries
{
    public sealed class GetIssuedWifiQueryHandler(
        IIssuedWifiQueryStore store,
        ICurrentTenantUser currentUser,
        PaginationOptions paginationOptions)
        : IQueryHandler<GetIssuedWifiQuery, OffsetPage<IssuedWifiItem>>
    {
        public async Task<Result<OffsetPage<IssuedWifiItem>>> Handle(
            GetIssuedWifiQuery query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<OffsetPage<IssuedWifiItem>>(scope.Error!);
            }

            if (scope.Value.HasValue && query.BranchId.HasValue && query.BranchId != scope.Value)
            {
                return Result.Failure<OffsetPage<IssuedWifiItem>>(WifiErrors.NotFound());
            }

            var pageSize = paginationOptions.ResolvePageSize(query.PageSize);
            var page = await store.ListAsync(
                new IssuedWifiListRequest(
                    query.Page,
                    pageSize,
                    query.IncludeTotalCount,
                    scope.Value ?? query.BranchId,
                    query.PackageId,
                    query.Status,
                    query.OrderNumber,
                    query.Date),
                cancellationToken);

            return Result.Success(OffsetPage<IssuedWifiItem>.Create(
                page.Entries.Select(IssuedWifiItem.From).ToArray(),
                query.Page,
                pageSize,
                page.TotalCount));
        }
    }
}
