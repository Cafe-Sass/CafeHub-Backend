namespace CafeHub.Application.Inventory.DTOs
{
    public sealed record InventoryAlertResponse(
        Guid Id,
        Guid ItemId,
        string ItemName,
        Guid BranchId,
        Guid? BatchId,
        string Kind,
        string State,
        decimal? Quantity,
        DateOnly? ExpiryDate,
        DateTimeOffset RaisedAt,
        DateTimeOffset? ResolvedAt,
        DateTimeOffset? NotifiedAt);
}
