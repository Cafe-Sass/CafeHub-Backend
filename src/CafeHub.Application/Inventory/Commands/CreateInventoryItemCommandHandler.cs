using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Inventory.Contracts;
using CafeHub.Application.Inventory.DTOs;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Inventory.Commands
{
    public sealed class CreateInventoryItemCommandHandler(
        IInventoryItemStore store, ICurrentTenantUser currentUser, ITenantContextAccessor tenantContext)
        : ICommandHandler<CreateInventoryItemCommand, InventoryItemResponse>
    {
        public Task<Result<InventoryItemResponse>> Handle(
            CreateInventoryItemCommand command, CancellationToken cancellationToken)
        {
            if (!InventoryItemActor.TryGetId(currentUser, tenantContext, out var actorId))
            {
                return Task.FromResult(
                    Result.Failure<InventoryItemResponse>(AuthErrors.CurrentTenantUserUnavailable()));
            }

            return store.CreateAsync(
                command.Name!,
                command.Unit,
                command.LowStockThreshold,
                command.TracksExpiry,
                command.NearExpiryDays ?? InventoryItem.DefaultNearExpiryDays,
                actorId,
                cancellationToken);
        }
    }
}
