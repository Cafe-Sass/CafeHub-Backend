using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Inventory.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Inventory.Commands
{
    public sealed record UpdateInventoryItemCommand(
        Guid Id,
        string? Name,
        InventoryUnit Unit,
        decimal LowStockThreshold,
        bool TracksExpiry,
        int NearExpiryDays,
        string ConcurrencyToken) : ICommand<InventoryItemResponse>, ITenantTransactionalCommand;
}
