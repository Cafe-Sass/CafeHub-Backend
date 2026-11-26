using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Inventory.DTOs;
using CafeHub.Application.Pagination;

namespace CafeHub.Application.Inventory.Queries
{
    public sealed record ListStockLevelsQuery : IQuery<OffsetPage<StockLevelResponse>>
    {
        public Guid? BranchId { get; init; }

        public Guid? ItemId { get; init; }

        public int Page { get; init; } = 1;

        public int? PageSize { get; init; }

        public bool IncludeTotalCount { get; init; }
    }
}
