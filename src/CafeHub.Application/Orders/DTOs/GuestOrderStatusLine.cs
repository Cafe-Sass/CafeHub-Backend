namespace CafeHub.Application.Orders.DTOs
{
    public sealed record GuestOrderStatusLine(
        string ProductName,
        string SizeName,
        int Quantity,
        IReadOnlyList<string> AddonNames);
}
