namespace CafeHub.Application.Refunds.DTOs
{
    public sealed record RefundRequestListItem(
        Guid Id,
        Guid BillId,
        Guid PaymentId,
        Guid BranchId,
        string PublicReference,
        string Status,
        string Reason,
        decimal ClaimedAmount,
        string? GuestDisplayName,
        Guid? AssignedUserId,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);
}
