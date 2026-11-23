using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Billing.DTOs;

namespace CafeHub.Application.Billing.Commands
{
    /// <summary>
    /// Replaces the cafe's online payment settings. Marked as a tenant transactional command so the
    /// settings update and the tenant audit entry commit together in one tenant transaction.
    /// </summary>
    public sealed record UpdatePaymentSettingsCommand(
        bool OnlineEnabled,
        string? MethodLabel,
        string? PaymentLink,
        string? PaymentAddress,
        string? DisplayName,
        string ConcurrencyToken) : ICommand<PaymentSettingsResponse>, ITenantTransactionalCommand;
}
