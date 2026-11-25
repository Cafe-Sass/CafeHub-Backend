using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Idempotency;
using CafeHub.Application.Inventory.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Inventory.Commands
{
    public sealed record RecordStockMovementCommand
        : ICommand<IReadOnlyList<StockMovementResponse>>, IIdempotentCommand, ITenantTransactionalCommand
    {
        public Guid BranchId { get; init; }

        public Guid ItemId { get; init; }

        public Guid? BatchId { get; init; }

        public StockMovementType Type { get; init; }

        public decimal Quantity { get; init; }

        public string? Reason { get; init; }

        public string? IdempotencyKey { get; set; }
    }
}
