using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Inventory.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Inventory.Contracts
{
    public interface IStockMovementStore
    {
        Task<Result<IReadOnlyList<StockMovementResponse>>> RecordAsync(
            Guid branchId,
            Guid itemId,
            Guid? batchId,
            StockMovementType type,
            decimal quantity,
            string reason,
            string idempotencyKey,
            Guid actorUserId,
            CancellationToken cancellationToken);

        Task<StockMovementListPage> ListAsync(
            StockMovementListRequest request, CancellationToken cancellationToken);
    }
}
