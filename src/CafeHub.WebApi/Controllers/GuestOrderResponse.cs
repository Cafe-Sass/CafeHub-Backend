namespace CafeHub.WebApi.Controllers
{
    public sealed record GuestOrderResponse(
        int OrderNumber,
        string PublicReference,
        string Status,
        decimal TotalAmount,
        DateTimeOffset ResponseDeadline);
}
