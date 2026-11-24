using CafeHub.Application.Billing.Contracts;

namespace CafeHub.Application.Billing.DTOs
{
    public sealed record BillListItem(
        Guid Id,
        Guid BranchId,
        Guid? TableId,
        int? TableNumber,
        string Status,
        string PublicReference,
        DateTimeOffset OpenedAt,
        DateTimeOffset? ClosedAt,
        decimal TotalAmount,
        decimal PaidAmount,
        decimal RemainingAmount)
    {
        public static BillListItem From(BillListEntry entry) =>
            new(
                entry.Id,
                entry.BranchId,
                entry.TableId,
                entry.TableNumber,
                entry.Status.ToString(),
                entry.PublicReference,
                entry.OpenedAt,
                entry.ClosedAt,
                entry.TotalAmount,
                entry.PaidAmount,
                entry.TotalAmount - entry.PaidAmount);
    }
}
