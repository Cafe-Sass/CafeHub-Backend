using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Billing
{
    public static class ReceiptComposer
    {
        public static Receipt Compose(
            Guid id,
            Bill bill,
            IReadOnlyList<Order> acceptedOrders,
            IReadOnlyList<Payment> payments,
            string receiptNumber,
            DateTimeOffset issuedAt,
            Guid? actorId)
        {
            ArgumentNullException.ThrowIfNull(bill);
            ArgumentNullException.ThrowIfNull(acceptedOrders);
            ArgumentNullException.ThrowIfNull(payments);

            var lines = acceptedOrders
                .OrderBy(order => order.OrderNumber)
                .ThenBy(order => order.Id)
                .SelectMany(order => order.Items
                    .OrderBy(item => item.LineNumber)
                    .Select(item => ReceiptLine.Create(
                        Guid.CreateVersion7(),
                        order.OrderNumber,
                        order.GuestDisplayName,
                        item.ProductName,
                        item.SizeName,
                        AddonsText(item.Addons),
                        item.Quantity,
                        item.UnitPrice,
                        item.LineTotal,
                        issuedAt)))
                .ToArray();
            var receiptPayments = payments
                .Where(payment => payment.Status == PaymentStatus.Confirmed)
                .OrderBy(payment => payment.ConfirmedAt)
                .ThenBy(payment => payment.Id)
                .Select(payment => ReceiptPayment.Create(
                    Guid.CreateVersion7(),
                    payment.Method,
                    payment.Amount,
                    payment.ConfirmedAt!.Value,
                    issuedAt))
                .ToArray();
            var totalAmount = Bill.ResolveTotalAmount(acceptedOrders);
            var paidAmount = Payment.ResolvePaidAmount(payments);

            return Receipt.Issue(
                id,
                bill.Id,
                bill.BranchId,
                receiptNumber,
                issuedAt,
                totalAmount,
                paidAmount,
                totalAmount - paidAmount,
                lines,
                receiptPayments,
                actorId);
        }

        private static string? AddonsText(IReadOnlyList<OrderItemAddon> addons) =>
            addons.Count == 0
                ? null
                : string.Join(", ", addons.Select(addon => addon.AddonName));
    }
}
