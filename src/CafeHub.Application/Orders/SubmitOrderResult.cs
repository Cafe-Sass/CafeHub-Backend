namespace CafeHub.Application.Orders
{
    public sealed record SubmitOrderResult(
        int OrderNumber,
        string PublicReference,
        string Status,
        decimal TotalAmount,
        DateTimeOffset ResponseDeadline);
}
