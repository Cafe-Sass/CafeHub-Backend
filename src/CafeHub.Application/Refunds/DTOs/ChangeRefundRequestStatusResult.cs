namespace CafeHub.Application.Refunds.DTOs
{
    public sealed record ChangeRefundRequestStatusResult(
        Guid RefundRequestId,
        string Status,
        string? ResolutionNote,
        DateTimeOffset? DecidedAt);
}
