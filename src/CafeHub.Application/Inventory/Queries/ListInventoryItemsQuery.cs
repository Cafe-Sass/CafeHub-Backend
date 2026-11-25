using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Inventory.DTOs;
using CafeHub.Application.Pagination;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Inventory.Queries
{
    public sealed record ListInventoryItemsQuery : IQuery<OffsetPage<InventoryItemResponse>>
    {
        public int Page { get; init; } = 1;

        public int? PageSize { get; init; }

        public bool IncludeTotalCount { get; init; }

        public InventoryItemStatus? Status { get; init; }
    }
}
