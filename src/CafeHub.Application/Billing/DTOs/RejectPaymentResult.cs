namespace CafeHub.Application.Billing.DTOs
{
    public sealed record RejectPaymentResult(
        Guid PaymentId,
        string Status,
        decimal BillPaidAmount,
        decimal BillRemainingAmount);
}
