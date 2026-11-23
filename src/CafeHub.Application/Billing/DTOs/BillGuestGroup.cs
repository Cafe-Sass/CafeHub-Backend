using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Billing.DTOs
{
    public sealed record BillGuestGroup(
        string GuestDisplayName,
        decimal TotalAmount,
        decimal PaidAmount,
        decimal RemainingAmount,
        IReadOnlyList<BillOrderView> Orders)
    {
        public static BillGuestGroup From(
            string guestDisplayName,
            IReadOnlyList<Order> orders,
            IReadOnlyList<Payment> payments)
        {
            ArgumentNullException.ThrowIfNull(orders);
            ArgumentNullException.ThrowIfNull(payments);

            var confirmedLinked = payments
                .Where(payment => payment.Status == PaymentStatus.Confirmed && payment.OrderId is not null)
                .ToArray();
            var totalAmount = orders.Sum(order => order.TotalAmount);
            var paidAmount = confirmedLinked
                .Where(payment => orders.Any(order => order.Id == payment.OrderId))
                .Sum(payment => payment.Amount);

            return new BillGuestGroup(
                guestDisplayName,
                totalAmount,
                paidAmount,
                totalAmount - paidAmount,
                orders
                    .OrderBy(order => order.OrderNumber)
                    .Select(order => BillOrderView.From(order, PaidFor(order, confirmedLinked)))
                    .ToArray());
        }

        private static decimal PaidFor(Order order, IReadOnlyList<Payment> confirmedLinked) =>
            confirmedLinked
                .Where(payment => payment.OrderId == order.Id)
                .Sum(payment => payment.Amount);
    }
}
