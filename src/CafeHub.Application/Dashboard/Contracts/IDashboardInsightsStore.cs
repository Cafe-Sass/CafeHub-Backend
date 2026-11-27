namespace CafeHub.Application.Dashboard.Contracts
{
    public sealed record DashboardInsightsRequest(
        Guid? BranchId,
        DateTimeOffset From,
        DateTimeOffset To);

    public sealed record DashboardProductSalesRow(
        Guid ProductId,
        string ProductName,
        int Quantity,
        decimal Sales);

    public sealed record DashboardInsightsSnapshot(
        IReadOnlyList<DashboardProductSalesRow> ProductSales,
        IReadOnlyList<DateTimeOffset> AcceptedOrderTimes);

    public interface IDashboardInsightsStore
    {
        Task<DashboardInsightsSnapshot> GetAsync(
            DashboardInsightsRequest request,
            CancellationToken cancellationToken);
    }
}
