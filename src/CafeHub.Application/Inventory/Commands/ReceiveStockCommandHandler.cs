using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Inventory.Contracts;
using CafeHub.Application.Inventory.DTOs;
using CafeHub.Application.Tenancy;

namespace CafeHub.Application.Inventory.Commands
{
    public sealed class ReceiveStockCommandHandler(
        IInventoryBatchStore store, ICurrentTenantUser currentUser, ITenantContextAccessor tenantContext)
        : ICommandHandler<ReceiveStockCommand, InventoryBatchResponse>
    {
        public Task<Result<InventoryBatchResponse>> Handle(
            ReceiveStockCommand command, CancellationToken cancellationToken)
        {
            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Task.FromResult(Result.Failure<InventoryBatchResponse>(scope.Error!));
            }

            if (scope.Value is { } scopedBranchId && scopedBranchId != command.BranchId)
            {
                return Task.FromResult(
                    Result.Failure<InventoryBatchResponse>(InventoryBatchErrors.BranchNotFound()));
            }

            if (!InventoryItemActor.TryGetId(currentUser, tenantContext, out var actorId))
            {
                return Task.FromResult(
                    Result.Failure<InventoryBatchResponse>(AuthErrors.CurrentTenantUserUnavailable()));
            }

            return store.ReceiveAsync(
                command.BranchId,
                command.ItemId,
                command.ReceivedQuantity,
                command.ReceivedDate,
                command.ExpiryDate,
                command.IdempotencyKey!,
                actorId,
                cancellationToken);
        }
    }
}
