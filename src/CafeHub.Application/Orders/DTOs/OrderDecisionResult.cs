namespace CafeHub.Application.Orders.DTOs
{
    public sealed record OrderDecisionResult(
        Guid OrderId,
        int OrderNumber,
        string Status,
        DateTimeOffset RespondedAt);
}
