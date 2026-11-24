using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Wifi.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Wifi.Commands
{
    /// <summary>
    /// Sets or clears one branch's free WiFi network details. A null or blank SSID clears the stored
    /// network; otherwise the SSID, security type, and optional guest-visible password are stored.
    /// </summary>
    public sealed record SetBranchWifiNetworkCommand(
        Guid BranchId,
        string? Ssid,
        WifiSecurity? Security,
        string? Password) : ICommand<WifiNetworkResponse?>, ITenantTransactionalCommand;
}
