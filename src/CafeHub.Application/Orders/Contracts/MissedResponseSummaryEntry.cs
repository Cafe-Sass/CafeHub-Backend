namespace CafeHub.Application.Orders.Contracts
{
    public sealed record MissedResponseSummaryEntry(Guid UserId, string FullName, int MissCount);
}
