using CafeHub.Application.Billing.Contracts;

namespace CafeHub.Application.Billing.DTOs
{
    public sealed record PaymentListItem(
        Guid Id,
        Guid BillId,
        string Method,
        string Status,
        decimal Amount,
        decimal? ClaimedAmount,
        string? Note,
        DateTimeOffset CreatedAt,
        DateTimeOffset? ConfirmedAt)
    {
        public static PaymentListItem From(PaymentListEntry entry) =>
            new(
                entry.Id,
                entry.BillId,
                entry.Method.ToString(),
                entry.Status.ToString(),
                entry.Amount,
                entry.ClaimedAmount,
                entry.Note,
                entry.CreatedAt,
                entry.ConfirmedAt);
    }
}
