namespace CafeHub.Application.Guests.Contracts
{
    public sealed record GuestMuteListPage(IReadOnlyList<GuestMuteEntry> Entries, int? TotalCount);
}
