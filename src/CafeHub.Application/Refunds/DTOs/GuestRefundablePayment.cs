namespace CafeHub.Application.Refunds.DTOs
{
    public sealed record GuestRefundablePayment(
        Guid PaymentId,
        string Method,
        decimal Amount,
        DateTimeOffset ConfirmedAt);
}
