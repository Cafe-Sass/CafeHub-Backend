namespace CafeHub.Application.Dashboard.Contracts
{
    public sealed record DashboardRevenueRequest(
        Guid? BranchId,
        DateTimeOffset From,
        DateTimeOffset To);

    public sealed record DashboardRevenueSnapshot(
        decimal Sales,
        decimal Collected,
        int Orders);

    public interface IDashboardRevenueStore
    {
        Task<DashboardRevenueSnapshot> GetAsync(
            DashboardRevenueRequest request,
            CancellationToken cancellationToken);
    }
}
