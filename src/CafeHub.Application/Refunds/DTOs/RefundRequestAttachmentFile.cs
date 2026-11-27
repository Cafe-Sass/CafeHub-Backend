namespace CafeHub.Application.Refunds.DTOs
{
    public sealed record RefundRequestAttachmentFile(Stream Content, string ContentType);
}
