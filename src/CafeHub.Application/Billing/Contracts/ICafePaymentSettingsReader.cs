using CafeHub.Application.Billing.DTOs;

namespace CafeHub.Application.Billing.Contracts
{
    /// <summary>
    /// Read port for the cafe's payment settings. The tenant is resolved from the write-once tenant
    /// context; the reader never accepts a tenant identifier from callers.
    /// </summary>
    public interface ICafePaymentSettingsReader
    {
        Task<PaymentSettingsResponse?> GetAsync(CancellationToken cancellationToken);

        Task<GuestPaymentOptionsResponse?> FindOptionsByQrCodeAsync(
            string qrCode,
            CancellationToken cancellationToken);
    }
}
