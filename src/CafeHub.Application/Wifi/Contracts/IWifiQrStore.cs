using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Wifi.DTOs;

namespace CafeHub.Application.Wifi.Contracts
{
    /// <summary>
    /// Persistence port for the branch WiFi QR. The management read creates the branch row lazily with
    /// the shared QR code generator; regeneration replaces the code in place so the old code stops
    /// resolving. The guest lookup is lock-free and returns only an Active QR on an Active branch.
    /// The code is never logged and never stored in an audit entry.
    /// </summary>
    public interface IWifiQrStore
    {
        Task<Result<WifiQrData>> GetOrCreateAsync(
            Guid? branchIdScope,
            Guid branchId,
            Guid actorUserId,
            CancellationToken cancellationToken);

        Task<Result<WifiQrData>> RegenerateAsync(
            Guid? branchIdScope,
            Guid branchId,
            Guid actorUserId,
            CancellationToken cancellationToken);

        Task<WifiQrContext?> ResolveAsync(string code, CancellationToken cancellationToken);
    }
}
