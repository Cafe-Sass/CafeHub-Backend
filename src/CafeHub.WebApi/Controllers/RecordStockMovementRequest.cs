namespace CafeHub.WebApi.Controllers
{
    public sealed record RecordStockMovementRequest(
        Guid ItemId,
        Guid? BatchId,
        string Type,
        decimal Quantity,
        string? Reason);
}
