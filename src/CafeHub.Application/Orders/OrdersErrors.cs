using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Orders
{
    public static class OrdersErrors
    {
        public const string PriceChangedCode = "Orders.PriceChanged";
        public const string ProductUnavailableCode = "Orders.ProductUnavailable";
        public const string OrderNumberConflictCode = "Orders.NumberConflict";
        public const string ReferenceConflictCode = "Orders.ReferenceConflict";

        public const string ItemsExtension = "items";
        public const string TotalExtension = "currentTotal";

        public static Error PriceChanged(
            IReadOnlyList<OrderPriceChange> changes, decimal currentTotal) =>
            Error.Conflict(PriceChangedCode, "The menu changed since it was read. Reload the menu and try again.") with
            {
                Details = new Dictionary<string, object?>
                {
                    [ItemsExtension] = changes,
                    [TotalExtension] = currentTotal,
                },
            };

        public static Error ProductUnavailable(IReadOnlyList<OrderUnavailableItem> items) =>
            Error.Conflict(ProductUnavailableCode, "One or more products are not available.") with
            {
                Details = new Dictionary<string, object?>
                {
                    [ItemsExtension] = items,
                },
            };

        public static Error OrderNumberConflict() =>
            Error.Conflict(OrderNumberConflictCode, "The order could not be numbered. Try again.");

        public static Error ReferenceConflict() =>
            Error.Conflict(ReferenceConflictCode, "The order reference could not be assigned. Try again.");
    }

    public sealed record OrderPriceChange(
        int LineNumber,
        Guid ProductId,
        Guid SizeId,
        decimal UnitPrice,
        IReadOnlyList<OrderAddonPrice> Addons,
        decimal LineTotal);

    public sealed record OrderAddonPrice(Guid ProductAddonId, string Name, decimal Price);

    public sealed record OrderUnavailableItem(int LineNumber, Guid ProductId);
}
