using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Inventory.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Inventory.Contracts
{
    public interface IInventoryItemStore
    {
        Task<Result<InventoryItemResponse>> CreateAsync(
            string name,
            InventoryUnit unit,
            decimal lowStockThreshold,
            bool tracksExpiry,
            int nearExpiryDays,
            Guid actorUserId,
            CancellationToken cancellationToken);

        Task<Result<InventoryItemResponse>> UpdateAsync(
            Guid id,
            string name,
            InventoryUnit unit,
            decimal lowStockThreshold,
            bool tracksExpiry,
            int nearExpiryDays,
            uint version,
            Guid actorUserId,
            CancellationToken cancellationToken);

        Task<Result<InventoryItemResponse>> ChangeStatusAsync(
            Guid id,
            InventoryItemStatus status,
            uint version,
            Guid actorUserId,
            CancellationToken cancellationToken);

        Task<InventoryItemResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

        Task<InventoryItemListPage> ListAsync(
            InventoryItemListRequest request, CancellationToken cancellationToken);
    }
}
