namespace CafeHub.Application.Dashboard.Contracts
{
    public sealed record DashboardLeakageRequest(
        Guid? BranchId,
        DateTimeOffset From,
        DateTimeOffset To);

    public sealed record DashboardLeakageRecord(
        Guid Id,
        decimal Total,
        DateTimeOffset OccurredAt);

    public sealed record DashboardLeakageBillRecord(
        Guid Id,
        decimal TotalAmount,
        decimal PaidAmount,
        DateTimeOffset ClosedAt);

    public sealed record DashboardLeakageSnapshot(
        IReadOnlyList<DashboardLeakageRecord> RejectedAndExpiredOrders,
        IReadOnlyList<DashboardLeakageBillRecord> ClosedBills,
        IReadOnlyList<DashboardLeakageRecord> RejectedPaymentProofs);

    public interface IDashboardLeakageStore
    {
        Task<DashboardLeakageSnapshot> GetAsync(
            DashboardLeakageRequest request,
            CancellationToken cancellationToken);
    }
}
