using System.Text.Json.Serialization;
using CafeHub.Domain.Tenants;

namespace CafeHub.WebApi.Controllers
{
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record SetBranchProductAvailabilityRequest(
        bool Available,
        string Reason = BranchProductAvailabilityReasons.Manual);
}
