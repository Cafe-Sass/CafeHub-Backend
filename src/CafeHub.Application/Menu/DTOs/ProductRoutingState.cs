using System.Text.Json.Serialization;

namespace CafeHub.Application.Menu.DTOs
{
    [JsonConverter(typeof(ProductRoutingStateConverter))]
    public enum ProductRoutingState
    {
        Assigned,
        Unassigned,
        StationInactive,
        BranchInactive,
    }

    internal sealed class ProductRoutingStateConverter()
        : JsonStringEnumConverter<ProductRoutingState>(namingPolicy: null, allowIntegerValues: false);
}
