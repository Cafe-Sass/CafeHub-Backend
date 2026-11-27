namespace CafeHub.Application.Dashboard.DTOs
{
    public sealed record DashboardPaymentSplit(decimal Cash, decimal Online);

    public sealed record DashboardRevenueMetrics(
        decimal Sales,
        decimal Collected,
        int Orders,
        decimal? AverageOrderValue,
        DashboardPaymentSplit PaymentSplit);

    public sealed record DashboardRevenueResponse(
        IReadOnlyList<DashboardMetricDefinition> MetricDefinitions,
        DashboardRevenueMetrics Metrics);
}
