namespace CafeHub.Application.Billing.DTOs
{
    public sealed record ReceiptLineView(
        string ProductName,
        string SizeName,
        string? Addons,
        int Quantity,
        decimal UnitPrice,
        decimal LineTotal);
}
