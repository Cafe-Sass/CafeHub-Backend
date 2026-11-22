using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Orders
{
    public sealed record OrderSubmissionError : Error, IErrorDetails
    {
        private OrderSubmissionError(
            string code, string message, IReadOnlyDictionary<string, object?> details)
            : base(code, message, ErrorType.Conflict)
        {
            Details = details;
        }

        public IReadOnlyDictionary<string, object?> Details { get; }

        public static OrderSubmissionError PriceChanged(
            IReadOnlyList<OrderPriceChange> changes, decimal currentTotal) =>
            new(
                OrdersErrors.PriceChangedCode,
                "The menu changed since it was read. Reload the menu and try again.",
                new Dictionary<string, object?>
                {
                    [OrdersErrors.ItemsExtension] = changes,
                    [OrdersErrors.TotalExtension] = currentTotal,
                });

        public static OrderSubmissionError ProductUnavailable(IReadOnlyList<OrderUnavailableItem> items) =>
            new(
                OrdersErrors.ProductUnavailableCode,
                "One or more products are not available.",
                new Dictionary<string, object?>
                {
                    [OrdersErrors.ItemsExtension] = items,
                });
    }
}
