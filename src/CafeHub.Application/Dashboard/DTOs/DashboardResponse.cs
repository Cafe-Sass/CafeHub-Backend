namespace CafeHub.Application.Dashboard.DTOs
{
    public sealed record DashboardBestSellers(
        IReadOnlyList<DashboardBestSeller> ByQuantity,
        IReadOnlyList<DashboardBestSeller> BySales);

    public sealed record DashboardOverviewMetrics(
        decimal Sales,
        decimal Collected,
        int Orders,
        decimal? AverageOrderValue,
        DashboardPaymentSplit PaymentSplit,
        IReadOnlyList<DashboardBestSeller> BestSellersByQuantity,
        IReadOnlyList<DashboardBestSeller> BestSellersBySales,
        IReadOnlyList<DashboardPeakHour> PeakHours,
        DashboardWifiAttachRate WifiAttachRate,
        IReadOnlyList<DashboardNearExpiryItem> NearExpiryItems);

    public sealed record DashboardLeakageSummary(int SignalCount);

    public sealed record DashboardResponse(
        IReadOnlyList<DashboardMetricDefinition> MetricDefinitions,
        DashboardOverviewMetrics Metrics,
        DashboardLeakageSummary Leakage);

    public sealed record DashboardMetricResponse(
        DashboardMetricDefinition Definition,
        object? Value);
}
