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

    public sealed record DashboardNearExpiryRow(
        Guid ItemId,
        string ItemName,
        Guid BranchId,
        Guid BatchId,
        DateOnly ExpiryDate);

    public sealed record DashboardInsightsSnapshot(
        IReadOnlyList<DashboardProductSalesRow> ProductSales,
        IReadOnlyList<DateTimeOffset> AcceptedOrderTimes,
        int AcceptedDineInOrders,
        int AcceptedDineInOrdersWithWifiPackage,
        IReadOnlyList<DashboardNearExpiryRow> NearExpiryItems);

    public interface IDashboardInsightsStore
    {
        Task<DashboardInsightsSnapshot> GetAsync(
            DashboardInsightsRequest request,
            CancellationToken cancellationToken);
    }
}
