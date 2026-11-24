using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Billing.DTOs;

namespace CafeHub.Application.Billing.Commands
{
    public sealed record RejectPaymentCommand : ICommand<RejectPaymentResult>, ITenantTransactionalCommand
    {
        public Guid PaymentId { get; init; }

        public string? Reason { get; init; }
    }
}
