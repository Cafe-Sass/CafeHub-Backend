using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Inventory
{
    public static class InventoryBatchErrors
    {
        public const string InvalidExpiryRangeCode = "Inventory.InvalidExpiryRange";
        public const string InvalidExpiryRangeMessage = "The expiry range start must not be after its end.";

        public static Error BranchNotFound() =>
            Error.NotFound("Inventory.BranchNotFound", "The branch was not found.");

        public static Error ItemInactive() =>
            Error.Conflict("Inventory.ItemInactive", "An inactive inventory item cannot receive stock.");

        public static Error ExpiryRequired() =>
            Error.Validation(
                "Inventory.ExpiryRequired", "An expiry date is required for an item that tracks expiry.");

        public static Error ExpiryNotTracked() =>
            Error.Validation(
                "Inventory.ExpiryNotTracked", "An item that does not track expiry cannot record an expiry date.");

        public static Error ExpiryBeforeReceived() =>
            Error.Validation(
                "Inventory.ExpiryBeforeReceived", "The expiry date must not be before the received date.");

        public static Error InvalidExpiryRange() =>
            Error.Validation(InvalidExpiryRangeCode, InvalidExpiryRangeMessage);
    }
}
