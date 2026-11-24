using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Wifi.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Wifi.Contracts
{
    /// <summary>
    /// Persistence port for the imported WiFi voucher pool. The caller classifies one batch before it
    /// writes; the insert and its counts-only audit entry commit together, and a duplicate database
    /// collision rolls the whole batch back with a retryable conflict. The summary is counts-only; no
    /// operation in this port returns a code value.
    /// </summary>
    public interface IWifiVoucherStore
    {
        Task<IReadOnlySet<string>> FindExistingCodesAsync(
            Guid packageId,
            IReadOnlyCollection<string> codes,
            CancellationToken cancellationToken);

        Task<Result> InsertImportedAsync(
            Guid packageId,
            IReadOnlyList<WifiVoucher> vouchers,
            WifiVoucherImportResult counts,
            Guid actorUserId,
            CancellationToken cancellationToken);

        Task<WifiVoucherSummary> GetSummaryAsync(
            Guid packageId,
            CancellationToken cancellationToken);

        Task<Result> VoidAsync(
            Guid packageId,
            string code,
            Guid actorUserId,
            CancellationToken cancellationToken);
    }
}
