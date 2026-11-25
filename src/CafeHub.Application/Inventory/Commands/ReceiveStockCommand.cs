using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Idempotency;
using CafeHub.Application.Inventory.DTOs;

namespace CafeHub.Application.Inventory.Commands
{
    public sealed record ReceiveStockCommand : ICommand<InventoryBatchResponse>, IIdempotentCommand, ITenantTransactionalCommand
    {
        public Guid BranchId { get; init; }

        public Guid ItemId { get; init; }

        public decimal ReceivedQuantity { get; init; }

        public DateOnly ReceivedDate { get; init; }

        public DateOnly? ExpiryDate { get; init; }

        public string? IdempotencyKey { get; set; }
    }
}
