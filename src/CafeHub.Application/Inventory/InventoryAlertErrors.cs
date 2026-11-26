using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Inventory
{
    public static class InventoryAlertErrors
    {
        public const string InvalidKindFilterCode = "Inventory.InvalidAlertKindFilter";
        public const string InvalidStateFilterCode = "Inventory.InvalidAlertStateFilter";

        public static Error InvalidKindFilter() =>
            Error.Validation(InvalidKindFilterCode, "The alert kind filter is not defined.");

        public static Error InvalidStateFilter() =>
            Error.Validation(InvalidStateFilterCode, "The alert state filter is not defined.");
    }
}
