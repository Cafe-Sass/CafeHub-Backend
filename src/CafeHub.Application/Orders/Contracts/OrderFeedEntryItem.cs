namespace CafeHub.Application.Orders.Contracts
{
    public sealed record OrderFeedEntryItem(
        string ProductName,
        string SizeName,
        int Quantity,
        Guid? StationId,
        string? StationName,
        IReadOnlyList<string> AddonNames);
}
