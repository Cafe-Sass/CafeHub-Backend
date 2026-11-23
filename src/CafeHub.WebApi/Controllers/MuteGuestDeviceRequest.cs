using System.Text.Json.Serialization;

namespace CafeHub.WebApi.Controllers
{
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record MuteGuestDeviceRequest(Guid OrderId, string? Reason, int? ExpiryHours);
}
