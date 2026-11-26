using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Inventory.Contracts;
using CafeHub.Application.Inventory.DTOs;
using CafeHub.Application.Tenancy;

namespace CafeHub.Application.Inventory.Commands
{
    public sealed class ReceiveStockCommandHandler(
        IInventoryBatchStore store, IInventoryAlertEvaluator evaluator,
        ICurrentTenantUser currentUser, ITenantContextAccessor tenantContext)
        : ICommandHandler<ReceiveStockCommand, InventoryBatchResponse>
    {
        public async Task<Result<InventoryBatchResponse>> Handle(
            ReceiveStockCommand command, CancellationToken cancellationToken)
        {
            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<InventoryBatchResponse>(scope.Error!);
            }

            if (scope.Value is { } scopedBranchId && scopedBranchId != command.BranchId)
            {
                return Result.Failure<InventoryBatchResponse>(InventoryBatchErrors.BranchNotFound());
            }

            if (!InventoryItemActor.TryGetId(currentUser, tenantContext, out var actorId))
            {
                return Result.Failure<InventoryBatchResponse>(AuthErrors.CurrentTenantUserUnavailable());
            }

            var result = await store.ReceiveAsync(
                command.BranchId,
                command.ItemId,
                command.ReceivedQuantity,
                command.ReceivedDate,
                command.ExpiryDate,
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
                ? Result.Failure<InventoryBatchResponse>(evaluated.Error!)
                : result;
        }
    }
}
