using CafeHub.Application.Abstractions.Results;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Orders
{
    public static class OrdersErrors
    {
        public const string PriceChangedCode = "Orders.PriceChanged";
        public const string ProductUnavailableCode = "Orders.ProductUnavailable";
        public const string OrderNumberConflictCode = "Orders.NumberConflict";
        public const string ServiceTypeInvalidCode = "Orders.ServiceTypeInvalid";
        public const string ReferenceConflictCode = "Orders.ReferenceConflict";
        public const string NotFoundCode = "Orders.NotFound";
        public const string StatusInvalidCode = "Orders.StatusInvalid";
        public const string BranchRequiredCode = "Orders.BranchRequired";
        public const string StatusConflictCode = "Orders.StatusConflict";
        public const string ResponseWindowElapsedCode = "Orders.ResponseWindowElapsed";

        public const string ItemsExtension = "items";
        public const string TotalExtension = "currentTotal";
        public const string CurrentStatusExtension = "currentStatus";

        public static OrderSubmissionError PriceChanged(
            IReadOnlyList<OrderPriceChange> changes, decimal currentTotal) =>
            OrderSubmissionError.PriceChanged(changes, currentTotal);

        public static OrderSubmissionError ProductUnavailable(IReadOnlyList<OrderUnavailableItem> items) =>
            OrderSubmissionError.ProductUnavailable(items);

        public static Error InvalidServiceType() =>
            Error.Validation(ServiceTypeInvalidCode, "The service type must be DineIn or Takeaway.");

        public static Error OrderNumberConflict() =>
            Error.Conflict(OrderNumberConflictCode, "The order could not be numbered. Try again.");

        public static Error ReferenceConflict() =>
            Error.Conflict(ReferenceConflictCode, "The order reference could not be assigned. Try again.");

        public static Error NotFound() =>
            Error.NotFound(NotFoundCode, "The order was not found.");

        public static Error StatusInvalid() =>
            Error.Validation(StatusInvalidCode, "The status filter must be Pending, Accepted, Rejected, or Expired.");

        public static Error BranchRequired() =>
            Error.Validation(BranchRequiredCode, "The branch id is required.");

        public static OrderDecisionError StatusConflict(OrderStatus currentStatus) =>
            OrderDecisionError.StatusConflict(currentStatus);

        public static Error ResponseWindowElapsed() =>
            Error.Conflict(
                ResponseWindowElapsedCode,
                "The response window has elapsed. The order can no longer be decided.");
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
