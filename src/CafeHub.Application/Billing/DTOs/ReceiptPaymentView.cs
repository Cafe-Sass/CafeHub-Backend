namespace CafeHub.Application.Billing.DTOs
{
    public sealed record ReceiptPaymentView(
        string Method,
        decimal Amount,
        DateTimeOffset ConfirmedAt);
}
