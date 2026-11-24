namespace CafeHub.Application.Billing.Contracts
{
    public sealed record PaymentListPage(IReadOnlyList<PaymentListEntry> Entries, int? TotalCount);
}
