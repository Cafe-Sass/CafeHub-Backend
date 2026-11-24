using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Idempotency;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Orders.Commands
{
    /// <summary>
    /// Buys one WiFi package alone from the branch WiFi QR. The order has no table, is accepted
    /// automatically, and receives its own new bill. The voucher is not assigned here; assignment
    /// happens on acceptance in CFMD-443.
    /// </summary>
    public sealed record SubmitWifiOrderCommand
        : ICommand<SubmitOrderResult>, ITenantTransactionalCommand, IIdempotentCommand, ISensitiveRequest
    {
        public string? IdempotencyKey { get; set; }

        public string? Code { get; init; }

        public Guid PackageId { get; init; }

        public string GuestDisplayName { get; init; } = string.Empty;
    }
}
