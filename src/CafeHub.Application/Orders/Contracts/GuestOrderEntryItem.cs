namespace CafeHub.Application.Orders.Contracts
{
    public sealed record GuestOrderEntryItem(
        string ProductName,
        string SizeName,
        int Quantity,
        IReadOnlyList<string> AddonNames);
}
