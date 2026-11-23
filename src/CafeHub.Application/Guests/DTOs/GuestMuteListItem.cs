using CafeHub.Application.Guests.Contracts;

namespace CafeHub.Application.Guests.DTOs
{
    public sealed record GuestMuteListItem(
        Guid Id,
        Guid OrderId,
        int OrderNumber,
        string Reason,
        Guid MutedByUserId,
        string MutedByName,
        DateTimeOffset CreatedAt,
        DateTimeOffset ExpiresAt,
        DateTimeOffset? LiftedAt,
        Guid? LiftedByUserId,
        string? LiftedByName,
        string? DeviceReference,
        bool IsActive)
    {
        public static GuestMuteListItem From(GuestMuteEntry entry, DateTimeOffset now) => new(
            entry.Id,
            entry.OrderId,
            entry.OrderNumber,
            entry.Reason,
            entry.MutedByUserId,
            entry.MutedByName,
            entry.CreatedAt,
            entry.ExpiresAt,
            entry.LiftedAt,
            entry.LiftedByUserId,
            entry.LiftedByName,
            MaskDeviceReference(entry.DeviceId),
            entry.LiftedAt is null && entry.ExpiresAt > now);

        public static string? MaskDeviceReference(string? deviceId) =>
            deviceId is not { Length: > 8 } ? null : $"{deviceId[..4]}\u2026{deviceId[^4..]}";
    }
}
