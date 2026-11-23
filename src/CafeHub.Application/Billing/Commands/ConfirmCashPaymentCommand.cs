using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Billing.DTOs;
using CafeHub.Application.Idempotency;

namespace CafeHub.Application.Billing.Commands
{
    public sealed record ConfirmCashPaymentCommand : ICommand<ConfirmCashPaymentResult>, IIdempotentCommand, ITenantTransactionalCommand
    {
        public Guid BillId { get; init; }

        public Guid? OrderId { get; init; }

        public decimal Amount { get; init; }

        public string? IdempotencyKey { get; set; }
    }
}
