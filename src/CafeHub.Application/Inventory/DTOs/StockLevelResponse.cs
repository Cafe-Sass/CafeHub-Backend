namespace CafeHub.Application.Inventory.DTOs
{
    public sealed record StockLevelResponse(
        Guid ItemId,
        Guid BranchId,
        decimal RemainingQuantity,
        DateOnly? NearestExpiryDate);
}
