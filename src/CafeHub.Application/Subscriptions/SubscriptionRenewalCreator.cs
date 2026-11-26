using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Notifications;
using CafeHub.Application.Onboarding.Contracts;
using CafeHub.Application.Subscriptions.Contracts;
using CafeHub.Domain.Plans;
using CafeHub.Domain.Subscriptions;

namespace CafeHub.Application.Subscriptions
{
    public sealed class SubscriptionRenewalCreator(
        ISubscriptionRenewalStore store,
        IOnboardingRequestStore onboardingRequests,
        INotificationSender notifications,
        SubscriptionOptions options,
        TimeProvider timeProvider)
    {
        public async Task<Result<SubscriptionRenewalRequest>> CreateAsync(
            Subscription subscription,
            Guid planVersionId,
            string? decidedBy,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(subscription);

            if (subscription.Status == SubscriptionStatus.Cancelled)
            {
                return Result.Failure<SubscriptionRenewalRequest>(SubscriptionErrors.RenewalNotAllowed());
            }

            if (await store.FindOpenRequestAsync(subscription.Id, cancellationToken) is not null)
            {
                return Result.Failure<SubscriptionRenewalRequest>(
                    SubscriptionErrors.RenewalAlreadyOpen());
            }

            var planVersion = await store.FindPlanVersionAsync(planVersionId, cancellationToken);
            if (planVersion is null || planVersion.Status != PlanVersionStatus.Active)
            {
                return Result.Failure<SubscriptionRenewalRequest>(
                    SubscriptionErrors.PlanVersionNotAvailable());
            }

            var now = timeProvider.GetUtcNow();
            var request = SubscriptionRenewalRequest.Create(
                Guid.CreateVersion7(),
                subscription.Id,
                planVersion.Id,
                planVersion.Price,
                planVersion.CurrencyCode,
                now,
                now.AddDays(options.RenewalRequestLifetimeDays));
            store.AddRequest(request);

            await SendNoticeAsync(subscription, request, cancellationToken);
            return Result.Success(request);
        }

        private async Task SendNoticeAsync(
            Subscription subscription,
            SubscriptionRenewalRequest request,
            CancellationToken cancellationToken)
        {
            if (subscription.TenantId is not { } tenantId
                || subscription.OnboardingRequestId is not { } onboardingRequestId)
            {
                return;
            }

            var onboardingRequest = await onboardingRequests.FindByIdAsync(
                onboardingRequestId,
                cancellationToken);
            if (onboardingRequest is null || string.IsNullOrWhiteSpace(onboardingRequest.OwnerEmail))
            {
                return;
            }

            await notifications.SendAsync(
                NotificationMessage.RenewalRequested(
                    tenantId,
                    onboardingRequest.OwnerEmail,
                    request.Id,
                    request.ProposedPrice,
                    request.ProposedCurrencyCode,
                    request.ExpiresAt),
                cancellationToken);
        }
    }
}
