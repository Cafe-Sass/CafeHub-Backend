using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Inventory.DTOs;

namespace CafeHub.Application.Inventory.Commands
{
    public sealed record DeactivateInventoryItemCommand(Guid Id, string ConcurrencyToken)
        : ICommand<InventoryItemResponse>, ITenantTransactionalCommand;
}
