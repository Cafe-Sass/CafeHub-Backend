using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Billing.DTOs;

namespace CafeHub.Application.Billing.Commands
{
    public sealed record CloseBillCommand : ICommand<BillDetail>, ITenantTransactionalCommand
    {
        public Guid BillId { get; init; }

        public string? Reason { get; init; }
    }
}
