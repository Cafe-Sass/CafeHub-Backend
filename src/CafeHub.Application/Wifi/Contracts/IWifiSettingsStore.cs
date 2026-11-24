using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Wifi.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Wifi.Contracts
{
    /// <summary>
    /// Read and write port for the cafe WiFi policy and the per-branch free network details. Reads are
    /// scoped by the caller's branch when a branch id is supplied; a write and its audit entry commit in
    /// one tenant transaction, and a stale policy concurrency token conflicts. A missing settings row
    /// is the default Free policy. The network password is guest-visible business data and is never
    /// included in an audit snapshot.
    /// </summary>
    public interface IWifiSettingsStore
    {
        Task<WifiSettingsResponse> GetAsync(Guid? branchId, CancellationToken cancellationToken);

        Task<Result<WifiSettingsResponse>> UpdatePolicyAsync(
            WifiPolicy policy,
            string concurrencyToken,
            Guid actorUserId,
            Guid? branchId,
            CancellationToken cancellationToken);

        Task<Result<WifiNetworkResponse?>> SetNetworkAsync(
            Guid? branchIdScope,
            Guid branchId,
            string? ssid,
            WifiSecurity? security,
            string? password,
            Guid actorUserId,
            CancellationToken cancellationToken);
    }
}
