namespace CafeHub.Application.Dashboard
{
    public static class DashboardLeakageCatalog
    {
        public static DashboardMetricDefinition? Find(string? key) =>
            All.FirstOrDefault(definition => string.Equals(definition.Key, key, StringComparison.Ordinal));

        public static IReadOnlyList<DashboardMetricDefinition> All { get; } = Array.AsReadOnly(
            new[]
            {
                new DashboardMetricDefinition(
                    DashboardLeakageKeys.RejectedAndExpiredOrders,
                    "Rejected and expired orders",
                    "Sum of order totals at submission for Rejected and Expired orders, selected by decision time (responded_at for Rejected, the expiry moment recorded in updated_at for Expired), inside the cafe-local range.",
                    new[] { "Rejected orders", "Expired orders" },
                    new[] { "Accepted and Pending orders", "payments", "refunds" }),
                new DashboardMetricDefinition(
                    DashboardLeakageKeys.BillsClosedWithAmountOwed,
                    "Bills closed with money still owed",
                    "Sum of the remaining amount at closing, the bill's Accepted order total minus its Confirmed payments, for bills closed inside the cafe-local range with a positive remaining amount.",
                    new[] { "Closed bills whose Accepted order total exceeds their Confirmed payments" },
                    new[] { "Open bills", "fully paid closed bills", "PendingReview and Rejected payments" }),
                new DashboardMetricDefinition(
                    DashboardLeakageKeys.RejectedPaymentProofs,
                    "Rejected payment proofs",
                    "Sum of the claimed amounts of Online payment proofs rejected inside the cafe-local range, selected by rejection time (updated_at).",
                    new[] { "Rejected Online payment proofs" },
                    new[] { "Confirmed and PendingReview payments", "refunds" }),
                new DashboardMetricDefinition(
                    DashboardLeakageKeys.SalesCollectedGap,
                    "Sales minus Collected gap",
                    "The difference between the Accepted order total and the Confirmed payment total of bills closed inside the cafe-local range. It is a gap to investigate, not a verified loss.",
                    new[] { "Accepted orders attached to closed bills", "Confirmed payments of those bills" },
                    new[] { "Open bills", "PendingReview and Rejected payments", "refunds", "any profit or margin claim" }),
            });
    }
}
