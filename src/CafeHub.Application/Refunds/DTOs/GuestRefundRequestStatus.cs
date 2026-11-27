using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Refunds.DTOs
{
    public sealed record GuestRefundRequestStatus(
        string PublicReference,
        string Status,
        string? ResolutionNote)
    {
        public static GuestRefundRequestStatus From(
            string publicReference,
            RefundRequestStatus status,
            string? resolutionNote) =>
            new(
                publicReference,
                status.ToString(),
                status is RefundRequestStatus.Approved or RefundRequestStatus.Rejected
                    ? resolutionNote
                    : null);
    }
}
