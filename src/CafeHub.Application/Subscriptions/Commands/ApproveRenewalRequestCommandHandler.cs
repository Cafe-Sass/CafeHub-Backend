using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Onboarding.Contracts;
using CafeHub.Application.Onboarding.PaymentGateway;
using CafeHub.Application.Subscriptions.Contracts;
using CafeHub.Application.Subscriptions.DTOs;
using CafeHub.Domain.Plans;
using CafeHub.Domain.Subscriptions;

namespace CafeHub.Application.Subscriptions.Commands
{
    public sealed class ApproveRenewalRequestCommandHandler(
        ISubscriptionRenewalStore store,
        IPaymentTransactionStore paymentTransactions,
        IPaymentGateway paymentGateway,
        IMasterUnitOfWork unitOfWork,
        IPlatformAuditWriter auditWriter,
        TimeProvider timeProvider)
        : ICommandHandler<ApproveRenewalRequestCommand, SubscriptionRenewalResponse>
    {
        private const string SucceededAction = "subscription.renewal.approved.succeeded";
        private const string FailedAction = "subscription.renewal.approved.failed";
        private const string ExpiredAction = "subscription.renewal.expired.succeeded";
        private const string TargetType = "subscription_renewal_request";

        public async Task<Result<SubscriptionRenewalResponse>> Handle(
            ApproveRenewalRequestCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var request = await store.FindRequestAsync(command.RenewalRequestId, cancellationToken);
            if (request is null)
            {
                return Result.Failure<SubscriptionRenewalResponse>(
                    SubscriptionErrors.RenewalRequestNotFound());
            }

            var subscription = await store.FindSubscriptionAsync(
                request.SubscriptionId, cancellationToken);
            if (subscription is null)
            {
                return Result.Failure<SubscriptionRenewalResponse>(SubscriptionErrors.NotFound());
            }

            if (subscription.TenantId != command.TenantKey)
            {
                return Result.Failure<SubscriptionRenewalResponse>(
                    SubscriptionErrors.RenewalRequestNotFound());
            }

            if (request.Status == RenewalRequestStatus.Completed)
            {
                var completedPeriod = await store.FindPeriodByRenewalRequestAsync(
                    request.Id, cancellationToken);
                return Result.Success(SubscriptionRenewalResponse.From(request, completedPeriod?.Id));
            }

            if (request.Status == RenewalRequestStatus.Declined)
            {
                return Result.Failure<SubscriptionRenewalResponse>(
                    SubscriptionErrors.RenewalNotApprovable());
            }

            if (request.Status == RenewalRequestStatus.Expired)
            {
                return Result.Failure<SubscriptionRenewalResponse>(
                    SubscriptionErrors.RenewalExpired());
            }

            if (subscription.Status == SubscriptionStatus.Cancelled)
            {
                return Result.Failure<SubscriptionRenewalResponse>(
                    SubscriptionErrors.RenewalNotAllowed());
            }

            var now = timeProvider.GetUtcNow();
            if (request.Status == RenewalRequestStatus.Pending && request.IsExpired(now))
            {
                await unitOfWork.BeginTransactionAsync(cancellationToken);
                try
                {
                    request.Expire(now);
                    auditWriter.Write(Entry(ExpiredAction, request.Id));
                    await unitOfWork.CommitTransactionAsync(cancellationToken);
                    return Result.Failure<SubscriptionRenewalResponse>(
                        SubscriptionErrors.RenewalExpired());
                }
                catch (Exception)
                {
                    await unitOfWork.RollbackTransactionAsync(cancellationToken);
                    throw;
                }
            }

            var planVersion = await store.FindPlanVersionAsync(
                request.PlanVersionId, cancellationToken);
            if (planVersion is null || planVersion.Status != PlanVersionStatus.Active)
            {
                return Result.Failure<SubscriptionRenewalResponse>(
                    SubscriptionErrors.PlanVersionNotAvailable());
            }

            var existingTransaction = await paymentTransactions.FindByRenewalRequestIdAsync(
                request.Id, cancellationToken);
            if (existingTransaction is not null
                && existingTransaction.Status != PlatformPaymentTransactionStatus.Succeeded)
            {
                return Result.Failure<SubscriptionRenewalResponse>(
                    SubscriptionErrors.RenewalChargeFailed());
            }

            await unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                if (request.Status == RenewalRequestStatus.Pending
                    && !request.Approve(now, command.ActorReference))
                {
                    await unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Failure<SubscriptionRenewalResponse>(
                        SubscriptionErrors.RenewalNotApprovable());
                }

                if (existingTransaction is null)
                {
                    var idempotencyKey = $"renewal-{request.Id:D}";
                    var charge = await paymentGateway.ChargeAsync(
                        new PaymentChargeRequest(
                            request.ProposedPrice,
                            request.ProposedCurrencyCode,
                            idempotencyKey,
                            request.Id.ToString()),
                        cancellationToken);
                    if (charge.IsFailure)
                    {
                        auditWriter.Write(Entry(FailedAction, request.Id));
                        await unitOfWork.CommitTransactionAsync(cancellationToken);
                        return Result.Failure<SubscriptionRenewalResponse>(charge.Error!);
                    }

                    var outcome = charge.Value.Outcome;
                    var transactionStatus = outcome == PaymentOutcome.Succeeded
                        ? PlatformPaymentTransactionStatus.Succeeded
                        : PlatformPaymentTransactionStatus.Failed;
                    var transaction = PlatformPaymentTransaction.Create(
                        Guid.CreateVersion7(),
                        null,
                        request.Id,
                        idempotencyKey,
                        request.ProposedPrice,
                        request.ProposedCurrencyCode,
                        transactionStatus,
                        outcome == PaymentOutcome.Succeeded ? now : null,
                        charge.Value.FailureCode,
                        now);
                    await paymentTransactions.AddAsync(transaction, cancellationToken);

                    if (outcome != PaymentOutcome.Succeeded)
                    {
                        auditWriter.Write(Entry(FailedAction, request.Id));
                        await unitOfWork.CommitTransactionAsync(cancellationToken);
                        return Result.Failure<SubscriptionRenewalResponse>(
                            SubscriptionErrors.RenewalChargeFailed());
                    }
                }

                var latestPeriod = await store.FindLatestPeriodAsync(
                    subscription.Id, cancellationToken);
                var periodStartsAt = latestPeriod?.EndsAt is { } endsAt && endsAt > now
                    ? endsAt
                    : now;
                if (latestPeriod is { PeriodStatus: SubscriptionPeriodStatus.Active }
                    && !latestPeriod.End(periodStartsAt, now, null))
                {
                    await unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Failure<SubscriptionRenewalResponse>(
                        SubscriptionErrors.RenewalNotAllowed());
                }

                var periodNumber = (latestPeriod?.PeriodNumber ?? 0) + 1;
                var period = SubscriptionPeriod.Create(
                    Guid.CreateVersion7(),
                    subscription.Id,
                    request.PlanVersionId,
                    periodNumber,
                    SubscriptionPeriodStatus.Active,
                    request.ProposedPrice,
                    request.ProposedCurrencyCode,
                    periodStartsAt,
                    SubscriptionRenewalPricing.NextEnd(periodStartsAt, planVersion.BillingPeriod),
                    request.Id,
                    now);
                store.AddPeriod(period);

                if (subscription.Status != SubscriptionStatus.Active)
                {
                    var previousStatus = subscription.Status;
                    if (!subscription.TransitionTo(SubscriptionStatus.Active, now, null))
                    {
                        await unitOfWork.RollbackTransactionAsync(cancellationToken);
                        return Result.Failure<SubscriptionRenewalResponse>(
                            SubscriptionErrors.RenewalNotAllowed());
                    }

                    store.AddHistory(SubscriptionHistory.Create(
                        Guid.CreateVersion7(),
                        subscription.Id,
                        previousStatus,
                        SubscriptionStatus.Active,
                        request.PlanVersionId,
                        periodNumber,
                        null,
                        command.ActorReference,
                        "renewal",
                        now));
                }

                if (!request.Complete(now))
                {
                    await unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Failure<SubscriptionRenewalResponse>(
                        SubscriptionErrors.RenewalNotApprovable());
                }

                auditWriter.Write(Entry(SucceededAction, request.Id));
                await unitOfWork.CommitTransactionAsync(cancellationToken);
                return Result.Success(SubscriptionRenewalResponse.From(request, period.Id));
            }
            catch (Exception)
            {
                await unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw;
            }
        }

        private static PlatformAuditEntry Entry(string action, Guid requestId) =>
            new()
            {
                ActorKind = PlatformAuditActorKind.System,
                Action = action,
                TargetType = TargetType,
                TargetId = requestId,
                Outcome = action.EndsWith(".failed", StringComparison.Ordinal)
                    ? PlatformAuditOutcome.Failed
                    : PlatformAuditOutcome.Succeeded,
            };
    }
}
