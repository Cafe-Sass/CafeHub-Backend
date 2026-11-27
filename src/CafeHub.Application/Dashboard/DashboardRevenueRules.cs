using CafeHub.Application.Dashboard.Contracts;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Dashboard
{
    public static class DashboardRevenueRules
    {
        public static IQueryable<Order> WhereAcceptedInRange(
            this IQueryable<Order> orders,
            DashboardRevenueRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            return orders.WhereAcceptedInRange(request.BranchId, request.From, request.To);
        }

        public static IQueryable<Order> WhereAcceptedInRange(
            this IQueryable<Order> orders,
            Guid? branchId,
            DateTimeOffset from,
            DateTimeOffset to)
        {
            ArgumentNullException.ThrowIfNull(orders);

            return orders.Where(order =>
                order.Status == OrderStatus.Accepted
                && (order.RespondedAt ?? order.SubmittedAt) >= from
                && (order.RespondedAt ?? order.SubmittedAt) < to
                && (branchId == null || order.BranchId == branchId));
        }

        public static IQueryable<Payment> WhereConfirmedInRange(
            this IQueryable<Payment> payments,
            DashboardRevenueRequest request)
        {
            ArgumentNullException.ThrowIfNull(payments);
            ArgumentNullException.ThrowIfNull(request);

            return payments.Where(payment =>
                payment.Status == PaymentStatus.Confirmed
                && payment.ConfirmedAt >= request.From
                && payment.ConfirmedAt < request.To);
        }

        public static IQueryable<Order> WhereBillableOrders(this IQueryable<Order> orders)
        {
            ArgumentNullException.ThrowIfNull(orders);

            return orders.Where(order => order.ServiceType != OrderServiceType.WifiOnly);
        }
    }
}
