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

    public sealed record DashboardSalesInsightsMetrics(
        IReadOnlyList<DashboardBestSeller> BestSellersByQuantity,
        IReadOnlyList<DashboardBestSeller> BestSellersBySales,
        IReadOnlyList<DashboardPeakHour> PeakHours);

    public sealed record DashboardSalesInsightsResponse(
        IReadOnlyList<DashboardMetricDefinition> MetricDefinitions,
        DashboardSalesInsightsMetrics Metrics);
}
