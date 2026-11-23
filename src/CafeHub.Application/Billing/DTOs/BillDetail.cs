using CafeHub.Application.Billing.Contracts;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Billing.DTOs
{
    /// <summary>
    /// One bill with its orders grouped by guest display name. The bill totals reconcile from the accepted
    /// orders and the confirmed payments; a person's paid amount counts only the confirmed payments linked to
    /// that person's orders, so a bill-level payment reduces the bill's remaining amount but no person's.
    /// <see cref="SettlementType"/> is null until money is recorded, because Cash, Online, and Hybrid are
    /// derived from the methods present and never stored.
    /// </summary>
    public sealed record BillDetail(
        Guid Id,
        Guid BranchId,
        Guid TableId,
        string Status,
        string PublicReference,
        DateTimeOffset OpenedAt,
        DateTimeOffset? ClosedAt,
        string? CloseReason,
        decimal TotalAmount,
        decimal PaidAmount,
        decimal RemainingAmount,
        string? SettlementType,
        IReadOnlyList<BillGuestGroup> Groups)
    {
        public const string NeutralGuestName = "Guest";

        public static BillDetail From(BillSettlement settlement)
        {
            ArgumentNullException.ThrowIfNull(settlement);

            var totalAmount = Bill.ResolveTotalAmount(settlement.AcceptedOrders);
            var paidAmount = Payment.ResolvePaidAmount(settlement.Payments);
            var groups = settlement.AcceptedOrders
                .GroupBy(order => NormalizeGuestName(order.GuestDisplayName), StringComparer.Ordinal)
                .Select(group => BillGuestGroup.From(group.Key, group.ToArray(), settlement.Payments))
                .OrderBy(group => group.GuestDisplayName, StringComparer.Ordinal)
                .ToArray();

            return new BillDetail(
                settlement.Bill.Id,
                settlement.Bill.BranchId,
                settlement.Bill.TableId,
                settlement.Bill.Status.ToString(),
                settlement.Bill.PublicReference,
                settlement.Bill.OpenedAt,
                settlement.Bill.ClosedAt,
                settlement.Bill.CloseReason,
                totalAmount,
                paidAmount,
                totalAmount - paidAmount,
                settlement.Payments.Any(payment => payment.Status == PaymentStatus.Confirmed)
                    ? Payment.ResolveSettlementType(settlement.Payments).ToString()
                    : null,
                groups);
        }

        private static string NormalizeGuestName(string? guestDisplayName) =>
            string.IsNullOrWhiteSpace(guestDisplayName) ? NeutralGuestName : guestDisplayName.Trim();
    }
}
