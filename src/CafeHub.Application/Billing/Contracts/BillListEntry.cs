using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Billing.Contracts
{
    public sealed record BillListEntry(
        Guid Id,
        Guid BranchId,
        Guid? TableId,
        int? TableNumber,
        BillStatus Status,
        string PublicReference,
        DateTimeOffset OpenedAt,
        DateTimeOffset? ClosedAt,
        decimal TotalAmount,
        decimal PaidAmount);
}
