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
        Guid? LiftedByUserId)
    {
        public string? DeviceId { get; init; }

        public string MutedByName { get; init; } = string.Empty;

        public string? LiftedByName { get; init; }
    }
}
