namespace CafeHub.Application.Menu.DTOs
{
    public sealed record ProductStationAssignmentResponse(
        Guid ProductId, Guid BranchId, ProductStationAssignmentItem? Assignment, ProductRoutingState RoutingState, string ConcurrencyToken);
}
