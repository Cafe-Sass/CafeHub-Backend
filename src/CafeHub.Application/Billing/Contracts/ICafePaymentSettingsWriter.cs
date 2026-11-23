using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Billing.DTOs;

namespace CafeHub.Application.Billing.Contracts
{
    /// <summary>
    /// Write port for the cafe's payment settings. The update and its audit entry commit together in
    /// one tenant transaction; a stale concurrency token conflicts.
    /// </summary>
    public interface ICafePaymentSettingsWriter
    {
        Task<Result<PaymentSettingsResponse>> UpdateAsync(
            bool onlineEnabled,
            string? methodLabel,
            string? paymentLink,
            string? paymentAddress,
            string? displayName,
            Guid actorUserId,
            string concurrencyToken,
            CancellationToken cancellationToken);
    }
}
