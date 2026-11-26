namespace CafeHub.Application.Subscriptions.DTOs
{
    public sealed record SubscriptionLifecycleResponse(
        Guid SubscriptionId,
        string? PreviousStatus,
        string Status,
        DateTimeOffset OccurredAt);
}
