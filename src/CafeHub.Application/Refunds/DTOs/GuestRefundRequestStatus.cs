namespace CafeHub.Application.Refunds.DTOs
{
    public sealed record GuestRefundRequestStatus(
        string PublicReference,
        string Status,
        string? ResolutionNote);
}
