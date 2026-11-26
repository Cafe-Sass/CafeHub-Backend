namespace CafeHub.Application.Subscriptions.DTOs
{
    public sealed record SubscriptionOverviewResponse(
        Guid SubscriptionId,
        Guid? TenantId,
        string TenantStatus,
        string Status,
        Guid? PlanVersionId,
        string? PlanCode,
        string? PlanName,
        int? PlanVersionNumber,
        decimal? Price,
        string? CurrencyCode,
        int? PeriodNumber,
        string? PeriodStatus,
        DateTimeOffset? StartsAt,
        DateTimeOffset? EndsAt);
}
