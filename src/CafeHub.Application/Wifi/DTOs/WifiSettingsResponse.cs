using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Wifi.DTOs
{
    /// <summary>
    /// The cafe's WiFi policy and the free network details visible in the caller's branch scope. The
    /// policy is cafe-level; the password is guest-visible by design and the concurrency token only
    /// guards the policy, not the branch networks.
    /// </summary>
    public sealed record WifiSettingsResponse(
        WifiPolicy Policy,
        string ConcurrencyToken,
        IReadOnlyList<WifiNetworkResponse> Networks);
}
