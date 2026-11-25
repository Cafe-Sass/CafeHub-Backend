using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Inventory.Contracts;
using CafeHub.Application.Inventory.DTOs;
using CafeHub.Application.Tenancy;

namespace CafeHub.Application.Inventory.Commands
{
    public sealed class RecordStockMovementCommandHandler(
        IStockMovementStore store, ICurrentTenantUser currentUser, ITenantContextAccessor tenantContext)
        : ICommandHandler<RecordStockMovementCommand, IReadOnlyList<StockMovementResponse>>
    {
        public Task<Result<IReadOnlyList<StockMovementResponse>>> Handle(
            RecordStockMovementCommand command, CancellationToken cancellationToken)
        {
            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Task.FromResult(
                    Result.Failure<IReadOnlyList<StockMovementResponse>>(scope.Error!));
            }

            if (scope.Value is { } scopedBranchId && scopedBranchId != command.BranchId)
            {
                return Task.FromResult(
                    Result.Failure<IReadOnlyList<StockMovementResponse>>(InventoryBatchErrors.BranchNotFound()));
            }

            if (!InventoryItemActor.TryGetId(currentUser, tenantContext, out var actorId))
            {
                return Task.FromResult(
                    Result.Failure<IReadOnlyList<StockMovementResponse>>(
                        AuthErrors.CurrentTenantUserUnavailable()));
            }

            return store.RecordAsync(
                command.BranchId,
                command.ItemId,
                command.BatchId,
                command.Type,
                command.Quantity,
                command.Reason!.Trim(),
                command.IdempotencyKey!,
                actorId,
                cancellationToken);
        }
    }
}
