using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Inventory.DTOs;
using CafeHub.Application.Pagination;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Inventory.Queries
{
    public sealed record ListStockMovementsQuery : IQuery<OffsetPage<StockMovementResponse>>
    {
        public Guid? BranchId { get; init; }

        public Guid? ItemId { get; init; }

        public StockMovementType? Type { get; init; }

        public DateOnly? CreatedFrom { get; init; }

        public DateOnly? CreatedTo { get; init; }

        public int Page { get; init; } = 1;

        public int? PageSize { get; init; }

        public bool IncludeTotalCount { get; init; }
    }
}
