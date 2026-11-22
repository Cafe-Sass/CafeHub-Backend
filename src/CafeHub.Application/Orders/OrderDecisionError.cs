using CafeHub.Application.Abstractions.Results;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Orders
{
    public sealed record OrderDecisionError : Error, IErrorDetails
    {
        private OrderDecisionError(
            string code,
            string message,
            IReadOnlyDictionary<string, object?> details)
            : base(code, message, ErrorType.Conflict)
        {
            Details = details;
        }

        public IReadOnlyDictionary<string, object?> Details { get; }

        public static OrderDecisionError StatusConflict(OrderStatus currentStatus) =>
            new(
                OrdersErrors.StatusConflictCode,
                "The order is not pending anymore.",
                new Dictionary<string, object?>
                {
                    [OrdersErrors.CurrentStatusExtension] = currentStatus.ToString(),
                });
    }
}
