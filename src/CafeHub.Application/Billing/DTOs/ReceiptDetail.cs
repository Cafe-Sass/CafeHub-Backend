using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Billing.DTOs
{
    /// <summary>
    /// The read model of an immutable receipt: its stable number and issue time, the grouped line
    /// snapshot, the payment snapshot, and the totals. It carries no internal ids and no staff
    /// identity, and it is a convenience record, never tax-compliance proof (FR-REC-002).
    /// </summary>
    public sealed record ReceiptDetail(
        string ReceiptNumber,
        DateTimeOffset IssuedAt,
        decimal TotalAmount,
        decimal PaidAmount,
        decimal RemainingAmount,
        IReadOnlyList<ReceiptLineGroup> Groups,
        IReadOnlyList<ReceiptPaymentView> Payments)
    {
        public static ReceiptDetail From(Receipt receipt)
        {
            ArgumentNullException.ThrowIfNull(receipt);

            var groups = receipt.Lines
                .GroupBy(line => ReceiptLine.NormalizeGuestName(line.GuestDisplayName), StringComparer.Ordinal)
                .Select(group => new ReceiptLineGroup(
                    group.Key,
                    group.Sum(line => line.LineTotal),
                    group
                        .OrderBy(line => line.OrderNumber)
                        .ThenBy(line => line.Id)
                        .Select(line => new ReceiptLineView(
                            line.ProductName,
                            line.SizeName,
                            line.Addons,
                            line.Quantity,
                            line.UnitPrice,
                            line.LineTotal))
                        .ToArray()))
                .OrderBy(group => group.GuestDisplayName, StringComparer.Ordinal)
                .ToArray();
            var payments = receipt.Payments
                .OrderBy(payment => payment.ConfirmedAt)
                .ThenBy(payment => payment.Id)
                .Select(payment => new ReceiptPaymentView(
                    payment.Method.ToString(),
                    payment.Amount,
                    payment.ConfirmedAt))
                .ToArray();

            return new ReceiptDetail(
                receipt.ReceiptNumber,
                receipt.IssuedAt,
                receipt.TotalAmount,
                receipt.PaidAmount,
                receipt.RemainingAmount,
                groups,
                payments);
        }
    }
}
