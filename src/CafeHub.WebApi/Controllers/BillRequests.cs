namespace CafeHub.WebApi.Controllers
{
    public sealed record ConfirmCashPaymentRequest(decimal Amount, Guid? OrderId);

    public sealed record CloseBillRequest(string? Reason);
}
