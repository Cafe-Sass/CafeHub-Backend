using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Billing.DTOs
{
    public sealed record BillOrderView(
        Guid Id,
        int OrderNumber,
        decimal TotalAmount,
        decimal PaidAmount,
        DateTimeOffset SubmittedAt)
    {
        public static BillOrderView From(Order order, decimal paidAmount)
        {
            ArgumentNullException.ThrowIfNull(order);

            return new BillOrderView(
                order.Id,
                order.OrderNumber,
                order.TotalAmount,
                paidAmount,
                order.SubmittedAt);
        }
    }
}
