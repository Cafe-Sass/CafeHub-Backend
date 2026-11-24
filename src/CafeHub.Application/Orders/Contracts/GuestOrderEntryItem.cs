namespace CafeHub.Application.Orders.Contracts
{
    public sealed record GuestOrderEntryItem(
        string ProductName,
        string SizeName,
        int Quantity,
        IReadOnlyList<string> AddonNames)
    {
        public int? WifiDurationMinutes { get; init; }

        public decimal? WifiSpeedMbps { get; init; }

        public string? WifiCode { get; init; }

        public bool WifiCodePending { get; init; }
    }
}
