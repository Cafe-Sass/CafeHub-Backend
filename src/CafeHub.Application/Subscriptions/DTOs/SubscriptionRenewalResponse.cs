using CafeHub.Domain.Subscriptions;

namespace CafeHub.Application.Subscriptions.DTOs
{
    public sealed record SubscriptionRenewalResponse(
        Guid Id,
        Guid SubscriptionId,
        string Status,
        Guid PlanVersionId,
        decimal ProposedPrice,
        string ProposedCurrencyCode,
        DateTimeOffset RequestedAt,
        DateTimeOffset ExpiresAt,
        DateTimeOffset? DecidedAt,
        string? DecidedBy,
        Guid? PeriodId)
    {
        public static SubscriptionRenewalResponse From(
            SubscriptionRenewalRequest request,
            Guid? periodId = null) =>
            new(
                request.Id,
                request.SubscriptionId,
                request.Status.ToString(),
                request.PlanVersionId,
                request.ProposedPrice,
                request.ProposedCurrencyCode,
                request.RequestedAt,
                request.ExpiresAt,
                request.DecidedAt,
                request.DecidedBy,
                periodId);
    }
}
