namespace CafeHub.Application.Orders.Contracts
{
    public sealed record MissedResponseSummaryPage(
        IReadOnlyList<MissedResponseSummaryEntry> Entries,
        int? TotalCount);
}
