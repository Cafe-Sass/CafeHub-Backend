using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Billing.DTOs;

namespace CafeHub.Application.Billing.Commands
{
    public sealed record ConfirmPaymentCommand : ICommand<ConfirmPaymentResult>, ITenantTransactionalCommand
    {
        public Guid PaymentId { get; init; }

        public decimal? Amount { get; init; }
    }
}
