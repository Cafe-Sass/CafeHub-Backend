namespace CafeHub.Application.Guests.Contracts
{
    public sealed record GuestMuteSummaryEntry(Guid UserId, string FullName, int MuteCount, int LiftedEarlyCount);
}
