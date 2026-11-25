using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Inventory.Contracts;
using CafeHub.Application.Inventory.DTOs;
using CafeHub.Application.Tenancy;

namespace CafeHub.Application.Inventory.Commands
{
    public sealed class UpdateInventoryItemCommandHandler(
        IInventoryItemStore store, ICurrentTenantUser currentUser, ITenantContextAccessor tenantContext)
        : ICommandHandler<UpdateInventoryItemCommand, InventoryItemResponse>
    {
        public Task<Result<InventoryItemResponse>> Handle(
            UpdateInventoryItemCommand command, CancellationToken cancellationToken)
        {
            if (!InventoryItemActor.TryGetId(currentUser, tenantContext, out var actorId))
            {
                return Task.FromResult(
                    Result.Failure<InventoryItemResponse>(AuthErrors.CurrentTenantUserUnavailable()));
            }

            if (!InventoryItemVersion.TryParse(command.ConcurrencyToken, out var version))
            {
                return Task.FromResult(Result.Failure<InventoryItemResponse>(InvalidToken()));
            }

            return store.UpdateAsync(
                command.Id,
                command.Name!,
                command.Unit,
                command.LowStockThreshold,
                command.TracksExpiry,
                command.NearExpiryDays,
                version,
                actorId,
                cancellationToken);
        }

        internal static Error InvalidToken() =>
            ValidationError.Create([new("concurrencyToken", "The concurrency token is invalid.")]);
    }
}
