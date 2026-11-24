namespace CafeHub.Application.Billing.DTOs
{
    public sealed record ReceiptLineGroup(
        string GuestDisplayName,
        decimal TotalAmount,
        IReadOnlyList<ReceiptLineView> Lines);
}
