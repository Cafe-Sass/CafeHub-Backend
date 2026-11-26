using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Inventory.Contracts
{
    public interface IInventoryAlertEvaluator
    {
        Task<Result> EvaluateStockAsync(
            Guid branchId, Guid itemId, CancellationToken cancellationToken);

        Task<Result> EvaluateExpiryAsync(
            Guid? branchId, Guid? itemId, CancellationToken cancellationToken);
    }
}
