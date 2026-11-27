namespace CafeHub.Application.Refunds.DTOs
{
    public sealed record RefundRequestAttachmentView(
        Guid Id,
        string ContentType,
        long SizeBytes,
        DateTimeOffset CreatedAt);
}
