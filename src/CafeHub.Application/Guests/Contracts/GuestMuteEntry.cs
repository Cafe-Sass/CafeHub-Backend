namespace CafeHub.Application.Guests.Contracts
{
    public sealed record GuestMuteEntry(
        Guid Id,
        Guid BranchId,
        Guid OrderId,
        int OrderNumber,
        Guid MutedByUserId,
        string Reason,
        DateTimeOffset CreatedAt,
        DateTimeOffset ExpiresAt,
        DateTimeOffset? LiftedAt,
        Guid? LiftedByUserId);
}
