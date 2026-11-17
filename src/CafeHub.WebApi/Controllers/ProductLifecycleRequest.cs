using System.Text.Json.Serialization;

namespace CafeHub.WebApi.Controllers
{
    public sealed record ProductLifecycleRequest([property: JsonRequired] string ConcurrencyToken);
}
