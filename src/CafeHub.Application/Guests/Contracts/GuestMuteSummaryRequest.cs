namespace CafeHub.Application.Guests.Contracts
{
    public sealed record GuestMuteSummaryRequest(Guid BranchId, DateTimeOffset From, DateTimeOffset ToExclusive);
}
