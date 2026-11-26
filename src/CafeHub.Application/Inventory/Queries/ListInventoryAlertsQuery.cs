using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Inventory.DTOs;
using CafeHub.Application.Pagination;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Inventory.Queries
{
    public sealed record ListInventoryAlertsQuery : IQuery<OffsetPage<InventoryAlertResponse>>
    {
        public InventoryAlertKind? Kind { get; init; }

        public InventoryAlertState? State { get; init; }

        public Guid? BranchId { get; init; }

        public int Page { get; init; } = 1;

        public int? PageSize { get; init; }

        public bool IncludeTotalCount { get; init; }
    }
}
