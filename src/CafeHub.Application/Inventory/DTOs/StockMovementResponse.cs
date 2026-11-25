using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Inventory.DTOs
{
    public sealed record StockMovementResponse(
        Guid Id,
        Guid ItemId,
        Guid BranchId,
        Guid BatchId,
        string Type,
        decimal Quantity,
        string? Reason,
        DateTimeOffset CreatedAt,
        Guid? CreatedByUserId)
    {
        public static StockMovementResponse From(StockMovement movement) =>
            new(
                movement.Id,
                movement.ItemId,
                movement.BranchId,
                movement.BatchId,
                movement.Type.ToString(),
                movement.Quantity,
                movement.Reason,
                movement.CreatedAt,
                movement.CreatedBy);
    }
}
