using CafeHub.Application.Dashboard.DTOs;

namespace CafeHub.Application.Dashboard
{
    public static class DashboardMetricValues
    {
        public static string? CanonicalKey(string? metric) =>
            DashboardMetricKeys.All.FirstOrDefault(
                key => string.Equals(key, metric?.Trim(), StringComparison.OrdinalIgnoreCase));

        public static object? Select(DashboardOverviewMetrics metrics, string key)
        {
            ArgumentNullException.ThrowIfNull(metrics);
            ArgumentNullException.ThrowIfNull(key);

            return key switch
            {
                DashboardMetricKeys.Sales => metrics.Sales,
                DashboardMetricKeys.Collected => metrics.Collected,
                DashboardMetricKeys.Orders => metrics.Orders,
                DashboardMetricKeys.AverageOrderValue => metrics.AverageOrderValue,
                DashboardMetricKeys.PaymentSplit => metrics.PaymentSplit,
                DashboardMetricKeys.BestSellers => new DashboardBestSellers(
                    metrics.BestSellersByQuantity,
                    metrics.BestSellersBySales),
                DashboardMetricKeys.PeakHours => metrics.PeakHours,
                DashboardMetricKeys.WifiAttachRate => metrics.WifiAttachRate,
                DashboardMetricKeys.NearExpiryItems => metrics.NearExpiryItems,
                _ => throw new ArgumentOutOfRangeException(nameof(key), key, "The metric key is not in the catalog."),
            };
        }
    }
}
