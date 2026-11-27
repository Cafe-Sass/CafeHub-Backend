namespace CafeHub.Application.Refunds.DTOs
{
    public sealed record RefundRequestEventView(
        Guid Id,
        string Kind,
        string? FromStatus,
        string? ToStatus,
        string ActorKind,
        Guid? ActorUserId,
        string? Note,
        DateTimeOffset OccurredAt);
}
