using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Inventory.Contracts;
using CafeHub.Application.Inventory.DTOs;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Inventory.Commands
{
    public sealed class DeactivateInventoryItemCommandHandler(
        IInventoryItemStore store, ICurrentTenantUser currentUser, ITenantContextAccessor tenantContext)
        : ICommandHandler<DeactivateInventoryItemCommand, InventoryItemResponse>
    {
        public Task<Result<InventoryItemResponse>> Handle(
            DeactivateInventoryItemCommand command, CancellationToken cancellationToken)
        {
            if (!InventoryItemActor.TryGetId(currentUser, tenantContext, out var actorId))
            {
                return Task.FromResult(
                    Result.Failure<InventoryItemResponse>(AuthErrors.CurrentTenantUserUnavailable()));
            }

            if (!InventoryItemVersion.TryParse(command.ConcurrencyToken, out var version))
            {
                return Task.FromResult(
                    Result.Failure<InventoryItemResponse>(UpdateInventoryItemCommandHandler.InvalidToken()));
            }

            return store.ChangeStatusAsync(
                command.Id, InventoryItemStatus.Inactive, version, actorId, cancellationToken);
        }
    }
}
