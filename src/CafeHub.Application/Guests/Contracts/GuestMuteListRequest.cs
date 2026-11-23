namespace CafeHub.Application.Guests.Contracts
{
    public sealed record GuestMuteListRequest(
        int Page,
        int PageSize,
        bool IncludeTotalCount,
        Guid BranchId,
        bool? IsActive,
        Guid? MutedByUserId,
        DateTimeOffset Now);
}
