using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Inventory.DTOs;

namespace CafeHub.Application.Inventory.Queries
{
    public sealed record GetInventoryItemQuery(Guid Id) : IQuery<InventoryItemResponse>;
}
