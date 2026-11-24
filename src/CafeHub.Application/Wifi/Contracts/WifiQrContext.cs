using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Wifi.Contracts
{
    /// <summary>
    /// The guest resolution snapshot of one WiFi QR: the branch, the cafe policy, the optional free
    /// network, and the package catalog (the application filters it to Active packages). Loaded only
    /// for an Active QR on an Active branch.
    /// </summary>
    public sealed record WifiQrContext(
        Guid BranchId,
        WifiPolicy Policy,
        BranchWifiNetwork? Network,
        IReadOnlyList<WifiPackage> Packages);
}
