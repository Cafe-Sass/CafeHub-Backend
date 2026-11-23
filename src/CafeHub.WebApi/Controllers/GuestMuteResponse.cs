using CafeHub.Application.Guests.DTOs;

namespace CafeHub.WebApi.Controllers
{
    public sealed record GuestMuteResponse(
        Guid Id,
        Guid OrderId,
        int OrderNumber,
        string Reason,
        DateTimeOffset CreatedAt,
        DateTimeOffset ExpiresAt,
        DateTimeOffset? LiftedAt,
        Guid MutedByUserId,
        string? MutedByName,
        string? LiftedByName)
    {
        public static GuestMuteResponse From(GuestMuteResult result) => new(
            result.Id,
            result.OrderId,
            result.OrderNumber,
            result.Reason,
            result.CreatedAt,
            result.ExpiresAt,
            result.LiftedAt,
            result.MutedByUserId,
            result.MutedByName,
            result.LiftedByName);
    }
}
