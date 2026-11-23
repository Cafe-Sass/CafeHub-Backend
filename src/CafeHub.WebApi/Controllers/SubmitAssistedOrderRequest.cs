using System.Text.Json.Serialization;

namespace CafeHub.WebApi.Controllers
{
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record SubmitAssistedOrderRequest(
        Guid TableId,
        string? ServiceType,
        string? GuestDisplayName,
        decimal? ExpectedTotal,
        IReadOnlyList<SubmitGuestOrderLineRequest>? Lines);
}
