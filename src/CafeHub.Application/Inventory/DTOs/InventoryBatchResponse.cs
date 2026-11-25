using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Inventory.DTOs
{
    public sealed record InventoryBatchResponse(
        Guid Id,
        Guid ItemId,
        Guid BranchId,
        decimal ReceivedQuantity,
        decimal RemainingQuantity,
        DateOnly ReceivedDate,
        DateOnly? ExpiryDate,
        Guid ReceivedByUserId)
    {
        public static InventoryBatchResponse From(InventoryBatch batch) =>
            new(
                batch.Id,
                batch.ItemId,
                batch.BranchId,
                batch.ReceivedQuantity,
                batch.RemainingQuantity,
                batch.ReceivedDate,
                batch.ExpiryDate,
                batch.ReceivedByUserId);
    }
}
