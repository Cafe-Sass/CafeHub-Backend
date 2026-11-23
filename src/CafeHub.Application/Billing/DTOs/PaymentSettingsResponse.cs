using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Billing.DTOs
{
    /// <summary>
    /// The cafe's online payment settings read model returned to a holder of the settings permission.
    /// The link and the address are business data, not secrets; the concurrency token is opaque.
    /// </summary>
    public sealed record PaymentSettingsResponse(
        bool OnlineEnabled,
        string MethodLabel,
        string? PaymentLink,
        string? PaymentAddress,
        string? DisplayName,
        string ConcurrencyToken)
    {
        public static PaymentSettingsResponse From(CafePaymentSettings settings, string concurrencyToken)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(concurrencyToken);

            return new PaymentSettingsResponse(
                settings.OnlineEnabled,
                settings.MethodLabel,
                settings.PaymentLink,
                settings.PaymentAddress,
                settings.DisplayName,
                concurrencyToken);
        }
    }
}
