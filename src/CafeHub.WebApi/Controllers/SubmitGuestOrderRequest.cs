using System.Text.Json.Serialization;

namespace CafeHub.WebApi.Controllers
{
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record SubmitGuestOrderRequest(
        string? Code,
        string? ServiceType,
        string? GuestDisplayName,
        decimal? ExpectedTotal,
        IReadOnlyList<SubmitGuestOrderLineRequest>? Lines);
}
