namespace CafeHub.Application.Billing.DTOs
{
    public sealed record ConfirmCashPaymentResult(
        Guid PaymentId,
        decimal Amount,
        string Method,
        string Status,
        Guid? OrderId,
        decimal BillPaidAmount,
        decimal BillRemainingAmount,
        DateTimeOffset ConfirmedAt);
}
