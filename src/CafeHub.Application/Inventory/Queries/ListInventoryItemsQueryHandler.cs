using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Inventory.Contracts;
using CafeHub.Application.Inventory.DTOs;
using CafeHub.Application.Pagination;

namespace CafeHub.Application.Inventory.Queries
{
    public sealed class ListInventoryItemsQueryHandler(
        IInventoryItemStore store, PaginationOptions paginationOptions)
        : IQueryHandler<ListInventoryItemsQuery, OffsetPage<InventoryItemResponse>>
    {
        public async Task<Result<OffsetPage<InventoryItemResponse>>> Handle(
            ListInventoryItemsQuery query, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var pageSize = paginationOptions.ResolvePageSize(query.PageSize);
            var listPage = await store.ListAsync(
                new InventoryItemListRequest(query.Status, query.Page, pageSize, query.IncludeTotalCount),
                cancellationToken);

            var page = OffsetPage<InventoryItemResponse>.Create(
                listPage.Items,
                query.Page,
                pageSize,
                listPage.TotalCount);

            return Result.Success(page);
        }
    }
}
