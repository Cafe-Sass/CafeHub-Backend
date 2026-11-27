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
            ArgumentNullException.ThrowIfNull(orders);
            ArgumentNullException.ThrowIfNull(request);

            return orders.Where(order =>
                order.Status == OrderStatus.Accepted
                && order.RespondedAt >= request.From
                && order.RespondedAt < request.To
                && (request.BranchId == null || order.BranchId == request.BranchId));
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
    }
}
