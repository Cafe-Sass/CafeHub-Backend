namespace CafeHub.Application.Dashboard.DTOs
{
    public sealed record DashboardBestSeller(
        Guid ProductId,
        string Name,
        int Quantity,
        decimal Sales);

    public sealed record DashboardPeakHour(
        int Hour,
        int Orders);

    public sealed record DashboardWifiAttachRate(
        decimal? Rate,
        string? Reason);

    public sealed record DashboardNearExpiryItem(
        Guid ItemId,
        string ItemName,
        Guid BranchId,
        Guid BatchId,
        DateOnly ExpiryDate);

    public sealed record DashboardSalesInsightsMetrics(
        IReadOnlyList<DashboardBestSeller> BestSellersByQuantity,
        IReadOnlyList<DashboardBestSeller> BestSellersBySales,
        IReadOnlyList<DashboardPeakHour> PeakHours,
        DashboardWifiAttachRate WifiAttachRate,
        IReadOnlyList<DashboardNearExpiryItem> NearExpiryItems);

    public sealed record DashboardSalesInsightsResponse(
        IReadOnlyList<DashboardMetricDefinition> MetricDefinitions,
        DashboardSalesInsightsMetrics Metrics);
}
