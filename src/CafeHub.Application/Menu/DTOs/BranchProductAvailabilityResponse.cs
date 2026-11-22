namespace CafeHub.Application.Menu.DTOs
{
    public sealed record BranchProductAvailabilityResponse(
        Guid BranchId, Guid ProductId, bool Available, string Reason, DateTimeOffset UpdatedAt);
}
