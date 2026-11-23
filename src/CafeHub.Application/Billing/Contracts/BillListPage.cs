namespace CafeHub.Application.Billing.Contracts
{
    public sealed record BillListPage(IReadOnlyList<BillListEntry> Entries, int? TotalCount);
}
