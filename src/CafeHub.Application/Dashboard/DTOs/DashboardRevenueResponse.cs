namespace CafeHub.Application.Dashboard.DTOs
{
    public sealed record DashboardRevenueMetrics(
        decimal Sales,
        decimal Collected,
        int Orders);

    public sealed record DashboardRevenueResponse(
        IReadOnlyList<DashboardMetricDefinition> MetricDefinitions,
        DashboardRevenueMetrics Metrics);
}
