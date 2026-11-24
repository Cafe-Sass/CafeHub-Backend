namespace CafeHub.Application.Wifi.DTOs
{
    public sealed record WifiNetworkResponse(
        Guid BranchId,
        string Ssid,
        string Security,
        string? Password);
}
