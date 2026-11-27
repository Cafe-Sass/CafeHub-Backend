namespace CafeHub.Application.Dashboard
{
    public static class DashboardMetricCatalog
    {
        public static IReadOnlyList<DashboardMetricDefinition> All { get; } = Array.AsReadOnly(
            new[]
            {
                new DashboardMetricDefinition(
                    DashboardMetricKeys.Sales,
                    "Sales",
                    "Sum of Accepted order totals, selected by acceptance time (responded_at), inside the cafe-local range.",
                    new[] { "Accepted orders", "menu order totals", "WiFi package totals" },
                    new[] { "Pending, Rejected, and Expired orders", "refunds" }),
                new DashboardMetricDefinition(
                    DashboardMetricKeys.Collected,
                    "Collected",
                    "Sum of Confirmed payment amounts, selected by confirmation time (confirmed_at), inside the cafe-local range.",
                    new[] { "Confirmed Cash and Online payments" },
                    new[] { "PendingReview and Rejected payments", "refunds" }),
                new DashboardMetricDefinition(
                    DashboardMetricKeys.Orders,
                    "Orders",
                    "Count of Accepted orders, selected by acceptance time (responded_at), inside the cafe-local range.",
                    new[] { "Accepted orders, menu and WiFi" },
                    new[] { "Pending, Rejected, and Expired orders" }),
                new DashboardMetricDefinition(
                    DashboardMetricKeys.AverageOrderValue,
                    "Average order value",
                    "Sales divided by Accepted orders that are not WiFi-only, rounded to two decimals; null when that count is zero.",
                    new[] { "Sales as the numerator" },
                    new[] { "WiFi-only orders from the denominator", "refunds" }),
                new DashboardMetricDefinition(
                    DashboardMetricKeys.PaymentSplit,
                    "Payment split",
                    "Confirmed payment amounts grouped by method (Cash and Online), selected by confirmation time (confirmed_at), inside the cafe-local range.",
                    new[] { "Confirmed Cash and Online payments" },
                    new[] { "PendingReview and Rejected payments", "refunds" }),
            });
    }
}
