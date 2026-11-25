using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Inventory.DTOs;
using CafeHub.Application.Pagination;

namespace CafeHub.Application.Inventory.Queries
{
    public sealed record ListInventoryBatchesQuery : IQuery<OffsetPage<InventoryBatchResponse>>
    {
        public required Guid BranchId { get; init; }

        public Guid? ItemId { get; init; }

        public DateOnly? ExpiryFrom { get; init; }

        public DateOnly? ExpiryTo { get; init; }

        public int Page { get; init; } = 1;

        public int? PageSize { get; init; }

        public bool IncludeTotalCount { get; init; }
    }
}
