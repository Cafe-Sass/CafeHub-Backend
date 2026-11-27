using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Refunds.DTOs
{
    public sealed record RefundRequestDetail(
        Guid Id,
        Guid BillId,
        Guid PaymentId,
        Guid? OrderId,
        Guid BranchId,
        string PublicReference,
        string Status,
        string Reason,
        string? Details,
        decimal ClaimedAmount,
        string? GuestDisplayName,
        Guid? AssignedUserId,
        string? ResolutionNote,
        Guid? DecidedByUserId,
        DateTimeOffset? DecidedAt,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt,
        IReadOnlyList<RefundRequestEventView> Events,
        IReadOnlyList<RefundRequestAttachmentView> Attachments)
    {
        public static RefundRequestDetail From(RefundRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            return new RefundRequestDetail(
                request.Id,
                request.BillId,
                request.PaymentId,
                request.OrderId,
                request.BranchId,
                request.PublicReference,
                request.Status.ToString(),
                request.Reason,
                request.Details,
                request.ClaimedAmount,
                request.GuestDisplayName,
                request.AssignedUserId,
                request.ResolutionNote,
                request.DecidedByUserId,
                request.DecidedAt,
                request.CreatedAt,
                request.UpdatedAt,
                request.Events
                    .OrderBy(entry => entry.OccurredAt)
                    .ThenBy(entry => entry.Id)
                    .Select(entry => new RefundRequestEventView(
                        entry.Id,
                        entry.Kind.ToString(),
                        entry.FromStatus?.ToString(),
                        entry.ToStatus?.ToString(),
                        entry.ActorKind.ToString(),
                        entry.ActorUserId,
                        entry.Note,
                        entry.OccurredAt))
                    .ToArray(),
                request.Attachments
                    .OrderBy(attachment => attachment.CreatedAt)
                    .ThenBy(attachment => attachment.Id)
                    .Select(attachment => new RefundRequestAttachmentView(
                        attachment.Id,
                        attachment.ContentType,
                        attachment.SizeBytes,
                        attachment.CreatedAt))
                    .ToArray());
        }
    }
}
