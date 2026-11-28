using CafeHub.Application.Dashboard.Contracts;
using CafeHub.Application.Dashboard.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Dashboard
{
    public static class DashboardLeakageRules
    {
        public const int MaximumContributors = 50;

        public const string SignalNotice =
            "Each leakage entry is a signal to investigate, not a verified loss.";

        public static IQueryable<Order> WhereRejectedOrExpiredInRange(
            this IQueryable<Order> orders,
            Guid? branchId,
            DateTimeOffset from,
            DateTimeOffset to)
        {
            ArgumentNullException.ThrowIfNull(orders);

            return orders.Where(order =>
                (order.Status == OrderStatus.Rejected || order.Status == OrderStatus.Expired)
                && (order.RespondedAt ?? order.UpdatedAt) >= from
                && (order.RespondedAt ?? order.UpdatedAt) < to
                && (branchId == null || order.BranchId == branchId));
        }

        public static IQueryable<Bill> WhereClosedInRange(
            this IQueryable<Bill> bills,
            Guid? branchId,
            DateTimeOffset from,
            DateTimeOffset to)
        {
            ArgumentNullException.ThrowIfNull(bills);

            return bills.Where(bill =>
                bill.Status == BillStatus.Closed
                && bill.ClosedAt != null
                && bill.ClosedAt >= from
                && bill.ClosedAt < to
                && (branchId == null || bill.BranchId == branchId));
        }

        public static IQueryable<Payment> WhereRejectedInRange(
            this IQueryable<Payment> payments,
            DateTimeOffset from,
            DateTimeOffset to)
        {
            ArgumentNullException.ThrowIfNull(payments);

            return payments.Where(payment =>
                payment.Status == PaymentStatus.Rejected
                && payment.UpdatedAt >= from
                && payment.UpdatedAt < to);
        }

        public static DashboardLeakageSignal RejectedAndExpiredOrders(
            IReadOnlyList<DashboardLeakageRecord> records,
            int contributorPage) =>
            Build(DashboardLeakageKeys.RejectedAndExpiredOrders, records, contributorPage);

        public static DashboardLeakageSignal RejectedPaymentProofs(
            IReadOnlyList<DashboardLeakageRecord> records,
            int contributorPage) =>
            Build(DashboardLeakageKeys.RejectedPaymentProofs, records, contributorPage);

        public static DashboardLeakageSignal BillsClosedWithAmountOwed(
            IReadOnlyList<DashboardLeakageBillRecord> bills,
            int contributorPage) =>
            Build(
                DashboardLeakageKeys.BillsClosedWithAmountOwed,
                AmountOwed(bills),
                contributorPage);

        public static DashboardLeakageSignal SalesCollectedGap(
            IReadOnlyList<DashboardLeakageBillRecord> bills,
            int contributorPage)
        {
            ArgumentNullException.ThrowIfNull(bills);

            return Build(
                DashboardLeakageKeys.SalesCollectedGap,
                AmountOwed(bills),
                contributorPage,
                bills.Sum(bill => bill.TotalAmount - bill.PaidAmount));
        }

        private static IReadOnlyList<DashboardLeakageRecord> AmountOwed(
            IReadOnlyList<DashboardLeakageBillRecord> bills)
        {
            ArgumentNullException.ThrowIfNull(bills);

            return bills
                .Where(bill => bill.TotalAmount > bill.PaidAmount)
                .Select(bill => new DashboardLeakageRecord(
                    bill.Id,
                    bill.TotalAmount - bill.PaidAmount,
                    bill.ClosedAt))
                .ToList();
        }

        private static DashboardLeakageSignal Build(
            string key,
            IReadOnlyList<DashboardLeakageRecord> records,
            int contributorPage,
            decimal? value = null)
        {
            ArgumentNullException.ThrowIfNull(records);
            ArgumentOutOfRangeException.ThrowIfLessThan(contributorPage, 1);

            var ordered = records
                .OrderByDescending(record => record.OccurredAt)
                .ThenBy(record => record.Id)
                .ToList();

            var skip = (int)Math.Min((long)(contributorPage - 1) * MaximumContributors, ordered.Count);
            var contributors = ordered
                .Skip(skip)
                .Take(MaximumContributors)
                .Select(record => new DashboardLeakageContributor(
                    record.Id,
                    record.Total,
                    record.OccurredAt))
                .ToList();

            return new DashboardLeakageSignal(
                key,
                ordered.Count,
                value ?? ordered.Sum(record => record.Total),
                contributorPage,
                skip + MaximumContributors < ordered.Count,
                contributors);
        }
    }
}
