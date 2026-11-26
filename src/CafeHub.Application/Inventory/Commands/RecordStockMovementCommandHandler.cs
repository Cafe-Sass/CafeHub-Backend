using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Inventory.Contracts;
using CafeHub.Application.Inventory.DTOs;
using CafeHub.Application.Tenancy;

namespace CafeHub.Application.Inventory.Commands
{
    public sealed class RecordStockMovementCommandHandler(
        IStockMovementStore store, IInventoryAlertEvaluator evaluator,
        ICurrentTenantUser currentUser, ITenantContextAccessor tenantContext)
        : ICommandHandler<RecordStockMovementCommand, IReadOnlyList<StockMovementResponse>>
    {
        public async Task<Result<IReadOnlyList<StockMovementResponse>>> Handle(
            RecordStockMovementCommand command, CancellationToken cancellationToken)
        {
            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<IReadOnlyList<StockMovementResponse>>(scope.Error!);
            }

            if (scope.Value is { } scopedBranchId && scopedBranchId != command.BranchId)
            {
                return Result.Failure<IReadOnlyList<StockMovementResponse>>(
                    InventoryBatchErrors.BranchNotFound());
            }

            if (!InventoryItemActor.TryGetId(currentUser, tenantContext, out var actorId))
            {
                return Result.Failure<IReadOnlyList<StockMovementResponse>>(
                    AuthErrors.CurrentTenantUserUnavailable());
            }

            var result = await store.RecordAsync(
                command.BranchId,
                command.ItemId,
                command.BatchId,
                command.Type,
                command.Quantity,
                command.Reason!.Trim(),
                command.IdempotencyKey!,
                actorId,
                cancellationToken);
            if (result.IsFailure)
            {
                return result;
            }

            var evaluated = await evaluator.EvaluateStockAsync(
                command.BranchId, command.ItemId, cancellationToken);
            return evaluated.IsFailure
                ? Result.Failure<IReadOnlyList<StockMovementResponse>>(evaluated.Error!)
                : result;
        }
    }
}
