using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Inventory
{
    public static class StockMovementErrors
    {
        public const string InvalidTypeFilterCode = "Inventory.InvalidMovementTypeFilter";
        public const string InvalidDateRangeCode = "Inventory.InvalidMovementDateRange";

        public static Error InvalidType() =>
            Error.Validation(
                "Inventory.InvalidMovementType",
                "Only Consume, Waste, or Adjust movements are recorded manually.");

        public static Error ReasonRequired() =>
            Error.Validation(
                "Inventory.ReasonRequired", "A reason is required for a manual stock movement.");

        public static Error BatchRequired() =>
            Error.Validation(
                "Inventory.BatchRequired", "An adjustment must name the inventory batch.");

        public static Error BatchNotAllowed() =>
            Error.Validation(
                "Inventory.BatchNotAllowed", "Consume and waste allocate their batches automatically.");

        public static Error InvalidTypeFilter() =>
            Error.Validation(
                InvalidTypeFilterCode, "The movement type filter is not defined.");

        public static Error InvalidDateRange() =>
            Error.Validation(
                InvalidDateRangeCode, "The movement date range is invalid.");

        public static Error BatchNotFound() =>
            Error.NotFound("Inventory.BatchNotFound", "The inventory batch was not found.");

        public static Error InsufficientStock() =>
            Error.Validation(
                "Inventory.InsufficientStock",
                "The requested quantity exceeds the available stock for the branch and item.");

        public static Error InsufficientBatchStock() =>
            Error.Validation(
                "Inventory.InsufficientBatchStock",
                "The adjustment would reduce the batch below zero.");

        public static Error AdjustmentExceedsReceived() =>
            Error.Validation(
                "Inventory.AdjustmentExceedsReceived",
                "The adjustment would raise the batch above its received quantity.");

        public static Error ConcurrencyConflict() =>
            Error.Conflict(
                "Inventory.BatchConcurrencyConflict",
                "The inventory batch was modified. Retry the movement.");
    }
}
