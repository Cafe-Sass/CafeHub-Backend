namespace CafeHub.Application.Billing.DTOs
{
    public sealed record ConfirmPaymentResult(
        Guid PaymentId,
        string Status,
        decimal Amount,
        decimal ClaimedAmount,
        decimal BillPaidAmount,
        decimal BillRemainingAmount,
        DateTimeOffset ConfirmedAt);
}
