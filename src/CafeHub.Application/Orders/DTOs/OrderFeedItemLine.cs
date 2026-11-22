namespace CafeHub.Application.Orders.DTOs
{
    public sealed record OrderFeedItemLine(
        string ProductName,
        string SizeName,
        int Quantity,
        string? StationName,
        IReadOnlyList<string> AddonNames);
}
