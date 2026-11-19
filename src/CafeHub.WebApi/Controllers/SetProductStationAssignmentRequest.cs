using System.Text.Json.Serialization;

namespace CafeHub.WebApi.Controllers
{
    public sealed record SetProductStationAssignmentRequest(
        [property: JsonRequired] Guid? StationId,
        [property: JsonRequired] string ConcurrencyToken);
}
