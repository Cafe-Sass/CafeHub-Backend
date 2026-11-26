using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Subscriptions.Contracts;
using CafeHub.Domain.Plans;
using CafeHub.Domain.Subscriptions;

namespace CafeHub.Application.Subscriptions
{
    public sealed record SubscriptionNextPlanResult(
        SubscriptionRenewalRequest Request,
        bool Created);

    public sealed class SubscriptionNextPlanService(
        ISubscriptionRenewalStore store,
        SubscriptionRenewalCreator creator,
        TimeProvider timeProvider)
    {
        public async Task<Result<SubscriptionNextPlanResult>> ApplyAsync(
            Subscription subscription,
            Guid planVersionId,
            string? actorReference,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(subscription);

            var planVersion = await store.FindPlanVersionAsync(planVersionId, cancellationToken);
            if (planVersion is null || planVersion.Status != PlanVersionStatus.Active)
            {
                return Result.Failure<SubscriptionNextPlanResult>(
                    SubscriptionErrors.PlanVersionNotAvailable());
            }

            var openRequest = await store.FindOpenRequestAsync(
                subscription.Id, cancellationToken);
            if (openRequest is null)
            {
                var created = await creator.CreateAsync(
                    subscription, planVersion.Id, actorReference, cancellationToken);
                return created.IsFailure
                    ? Result.Failure<SubscriptionNextPlanResult>(created.Error!)
                    : Result.Success(new SubscriptionNextPlanResult(created.Value, true));
            }

            if (openRequest.Status != RenewalRequestStatus.Pending)
            {
                return Result.Failure<SubscriptionNextPlanResult>(
                    SubscriptionErrors.RenewalNotApprovable());
            }

            if (!openRequest.ChangePlan(
                planVersion.Id,
                planVersion.Price,
                planVersion.CurrencyCode,
                timeProvider.GetUtcNow()))
            {
                return Result.Failure<SubscriptionNextPlanResult>(
                    SubscriptionErrors.RenewalNotApprovable());
            }

            return Result.Success(new SubscriptionNextPlanResult(openRequest, false));
        }
    }
}
