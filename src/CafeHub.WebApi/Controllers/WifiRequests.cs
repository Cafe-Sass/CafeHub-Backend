namespace CafeHub.WebApi.Controllers
{
    public sealed record UpdateWifiSettingsRequest(string? Policy, string? ConcurrencyToken);

    public sealed record SetBranchWifiNetworkRequest(string? Ssid, string? Security, string? Password);
}
