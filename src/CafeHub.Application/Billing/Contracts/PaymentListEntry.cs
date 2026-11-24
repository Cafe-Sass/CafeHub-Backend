using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Billing.Contracts
{
    public sealed record PaymentListEntry(
        Guid Id,
        Guid BillId,
        PaymentMethod Method,
        PaymentStatus Status,
        decimal Amount,
        decimal? ClaimedAmount,
        string? Note,
        DateTimeOffset CreatedAt,
        DateTimeOffset? ConfirmedAt);
}
