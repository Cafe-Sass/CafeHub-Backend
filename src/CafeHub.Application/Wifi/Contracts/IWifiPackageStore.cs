using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Wifi.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Wifi.Contracts
{
    /// <summary>
    /// Persistence port for the paid WiFi packages. Reads and writes are cafe-level, policy-independent
    /// storage; the caller enforces the Paid-policy rule. The active-name check and the database unique
    /// index agree on the lower-cased name, and a write and its audit entry commit together.
    /// </summary>
    public interface IWifiPackageStore
    {
        Task<WifiPackageListPage> ListAsync(
            WifiPackageListRequest request,
            CancellationToken cancellationToken);

        Task<WifiPackageResponse?> GetAsync(Guid packageId, CancellationToken cancellationToken);

        Task<bool> ActiveNameExistsAsync(
            string name,
            Guid? excludePackageId,
            CancellationToken cancellationToken);

        Task<Result<WifiPackageResponse>> CreateAsync(
            WifiPackage package,
            CancellationToken cancellationToken);

        Task<Result<WifiPackageResponse>> UpdateAsync(
            Guid packageId,
            string concurrencyToken,
            string name,
            int durationMinutes,
            decimal? speedMbps,
            WifiDataMode dataMode,
            int? quotaMb,
            decimal price,
            int sortOrder,
            Guid actorUserId,
            CancellationToken cancellationToken);

        Task<Result<WifiPackageResponse>> ChangeStatusAsync(
            Guid packageId,
            WifiPackageStatus status,
            string concurrencyToken,
            Guid actorUserId,
            CancellationToken cancellationToken);
    }
}
