namespace CafeHub.Application.Subscriptions.DTOs
{
    public sealed record SubscriptionHistoryItem(
        Guid Id,
        string? FromStatus,
        string ToStatus,
        Guid? PlanVersionId,
        int? PeriodNumber,
        string? ActorReference,
        string? Reason,
        DateTimeOffset OccurredAt);
}
