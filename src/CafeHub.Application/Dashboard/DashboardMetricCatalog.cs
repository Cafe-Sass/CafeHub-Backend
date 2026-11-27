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
                    "Sum of Accepted order totals, selected by acceptance time (responded_at, or submitted_at for an automatically accepted WiFi-only order), inside the cafe-local range.",
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
                    "Count of Accepted orders, selected by acceptance time (responded_at, or submitted_at for an automatically accepted WiFi-only order), inside the cafe-local range.",
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
                new DashboardMetricDefinition(
                    DashboardMetricKeys.BestSellers,
                    "Best sellers",
                    "Top 20 menu products from Accepted orders, ranked by total quantity and by line sales, with ties broken by product name then product id.",
                    new[] { "Accepted orders", "order item name, quantity, and line total snapshots" },
                    new[] { "Pending, Rejected, and Expired orders", "WiFi package lines", "the live product catalog name" }),
                new DashboardMetricDefinition(
                    DashboardMetricKeys.PeakHours,
                    "Peak hours",
                    "Count of Accepted orders for each cafe-local hour of acceptance, with all 24 hours returned.",
                    new[] { "Accepted orders", "cafe-local hour of acceptance" },
                    new[] { "Pending, Rejected, and Expired orders" }),
                new DashboardMetricDefinition(
                    DashboardMetricKeys.WifiAttachRate,
                    "WiFi attach rate",
                    "Accepted dine-in orders with a WiFi package line divided by Accepted dine-in orders, rounded to four decimals; null with a reason under the Free policy or when there are no Accepted dine-in orders.",
                    new[] { "Accepted dine-in orders", "order item WiFi package snapshots" },
                    new[] { "Takeaway and WiFi-only orders", "the Free WiFi policy" }),
                new DashboardMetricDefinition(
                    DashboardMetricKeys.NearExpiryItems,
                    "Near-expiry items",
                    "Open NearExpiry stock alerts in the branch scope, with their item, branch, batch, and expiry date.",
                    new[] { "Open NearExpiry stock alerts" },
                    new[] { "Resolved alerts", "LowStock and Expired alerts" }),
            });
    }
}
