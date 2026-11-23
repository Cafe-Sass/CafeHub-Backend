using CafeHub.Application.Guests.Contracts;

namespace CafeHub.Application.Guests.DTOs
{
    public sealed record GuestMuteResult(
        Guid Id,
        Guid OrderId,
        int OrderNumber,
        string Reason,
        DateTimeOffset CreatedAt,
        DateTimeOffset ExpiresAt,
        DateTimeOffset? LiftedAt,
        Guid MutedByUserId)
    {
        public string? MutedByName { get; init; }

        public string? LiftedByName { get; init; }

        public static GuestMuteResult From(GuestMuteEntry entry) => new(
            entry.Id,
            entry.OrderId,
            entry.OrderNumber,
            entry.Reason,
            entry.CreatedAt,
            entry.ExpiresAt,
            entry.LiftedAt,
            entry.MutedByUserId)
        {
            MutedByName = entry.MutedByName,
            LiftedByName = entry.LiftedByName,
        };
    }
}
