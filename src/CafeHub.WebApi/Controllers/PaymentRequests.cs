namespace CafeHub.WebApi.Controllers
{
    public sealed record ConfirmPaymentRequest(decimal? Amount);

    public sealed record RejectPaymentRequest(string? Reason);
}
