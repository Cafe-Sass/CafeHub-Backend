namespace CafeHub.Application.Orders
{
    public sealed record StoredOrder(string? RequestHash, SubmitOrderResult Result);
}
