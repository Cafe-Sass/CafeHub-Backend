namespace CafeHub.Application.Menu.DTOs
{
    public sealed record BranchProductAvailabilityItem(
        Guid ProductId, string Name, Guid CategoryId, bool Available, string? Reason);
}
