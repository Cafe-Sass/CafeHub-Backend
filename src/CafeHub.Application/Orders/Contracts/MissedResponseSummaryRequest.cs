namespace CafeHub.Application.Orders.Contracts
{
    public sealed record MissedResponseSummaryRequest(
        int Page,
        int PageSize,
        bool IncludeTotalCount,
        Guid BranchId,
        DateOnly From,
        DateOnly To);
}
