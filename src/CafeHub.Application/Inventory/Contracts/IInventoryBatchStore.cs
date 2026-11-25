using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Inventory.DTOs;

namespace CafeHub.Application.Inventory.Contracts
{
    public interface IInventoryBatchStore
    {
        Task<Result<InventoryBatchResponse>> ReceiveAsync(
            Guid branchId,
            Guid itemId,
            decimal receivedQuantity,
            DateOnly receivedDate,
            DateOnly? expiryDate,
            string idempotencyKey,
            Guid actorUserId,
            CancellationToken cancellationToken);
    }
}
