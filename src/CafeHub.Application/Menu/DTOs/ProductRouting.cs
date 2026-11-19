namespace CafeHub.Application.Menu.DTOs
{
    public sealed record ProductRouting(ProductRoutingState State, Guid? StationId, bool ProductAvailable)
    {
        public bool CanOrder => ProductAvailable && State is (ProductRoutingState.Assigned or ProductRoutingState.Unassigned);
    }
}
