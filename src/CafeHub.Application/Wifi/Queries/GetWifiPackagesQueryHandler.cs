using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Pagination;
using CafeHub.Application.Wifi.Contracts;
using CafeHub.Application.Wifi.DTOs;

namespace CafeHub.Application.Wifi.Queries
{
    public sealed class GetWifiPackagesQueryHandler(
        IWifiPackageStore store,
        PaginationOptions paginationOptions)
        : IQueryHandler<GetWifiPackagesQuery, OffsetPage<WifiPackageListItem>>
    {
        public async Task<Result<OffsetPage<WifiPackageListItem>>> Handle(
            GetWifiPackagesQuery query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var pageSize = paginationOptions.ResolvePageSize(query.PageSize);
            var page = await store.ListAsync(
                new WifiPackageListRequest(
                    query.Page,
                    pageSize,
                    query.IncludeTotalCount,
                    query.Status),
                cancellationToken);

            return Result.Success(OffsetPage<WifiPackageListItem>.Create(
                page.Entries.Select(WifiPackageListItem.From).ToArray(),
                query.Page,
                pageSize,
                page.TotalCount));
        }
    }
}
