using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Inventory
{
    public static class InventoryItemErrors
    {
        public static Error NotFound() =>
            Error.NotFound("Inventory.ItemNotFound", "The inventory item was not found.");

        public static Error NameConflict() =>
            Error.Conflict("Inventory.ItemNameConflict", "An active inventory item already uses this name.");

        public static Error ConcurrencyConflict() =>
            Error.Conflict("Inventory.ConcurrencyConflict", "The inventory item was modified. Refresh and try again.");
    }
}
