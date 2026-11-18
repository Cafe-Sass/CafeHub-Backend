using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Menu
{
    public static class ProductErrors
    {
        public static Error NotFound() => Error.NotFound("Products.NotFound", "The product was not found.");

        public static Error ConcurrencyConflict() =>
            Error.Conflict("Products.ConcurrencyConflict", "The product was modified. Refresh and try again.");

        public static Error NameConflict() =>
            Error.Conflict("Products.NameConflict", "An active size or add-on already uses this name.");

        public static Error DefaultSizeConflict() =>
            Error.Conflict("Products.DefaultSizeConflict", "Another active default size already exists.");

        public static Error InvalidCategory() =>
            ValidationError.Create([new("categoryId", "The category is not active or does not exist.")]);

        public static Error CatalogSchemaNotReady() =>
            Error.Conflict("Products.CatalogSchemaNotReady", "The product catalog is awaiting tenant schema finalization.");

        public static Error RoutingNotReady() =>
            Error.Conflict("Products.RoutingNotReady", "Product routing is awaiting tenant migration verification.");

        public static Error InvalidStation() =>
            ValidationError.Create([new("stationId", "The station is not active or does not exist.")]);

        public static Error InvalidKind() =>
            ValidationError.Create([new("kind", "The kind must be Food or Drink.")]);

        public static Error InvalidSize() =>
            ValidationError.Create([new("sizes", "The sizes are invalid for this product.")]);

        public static Error InvalidAddon() =>
            ValidationError.Create([new("addons", "The add-ons are invalid for this product.")]);

        public static Error InvalidTags() =>
            ValidationError.Create([new("tags", "The tags are invalid for this product.")]);
    }
}
