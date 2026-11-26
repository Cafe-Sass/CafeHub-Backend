using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Subscriptions.Contracts;
using CafeHub.Application.Subscriptions.DTOs;
using CafeHub.Domain.Plans;
using CafeHub.Domain.Subscriptions;

namespace CafeHub.Application.Subscriptions.Commands
{
    public sealed class ChangeRenewalPlanCommandHandler(
        ISubscriptionRenewalStore store,
        SubscriptionRenewalCreator creator,
        IMasterUnitOfWork unitOfWork,
        IPlatformAuditWriter auditWriter,
        TimeProvider timeProvider)
        : ICommandHandler<ChangeRenewalPlanCommand, SubscriptionRenewalResponse>
    {
        private const string PlanChangedAction = "subscription.renewal.plan_changed.succeeded";
        private const string RequestedAction = "subscription.renewal.requested.succeeded";
        private const string TargetType = "subscription_renewal_request";

        public async Task<Result<SubscriptionRenewalResponse>> Handle(
            ChangeRenewalPlanCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var subscription = await store.FindSubscriptionAsync(
                command.SubscriptionId, cancellationToken);
            if (subscription is null)
            {
                return Result.Failure<SubscriptionRenewalResponse>(SubscriptionErrors.NotFound());
            }

            var planVersion = await store.FindPlanVersionAsync(
                command.PlanVersionId, cancellationToken);
            if (planVersion is null || planVersion.Status != PlanVersionStatus.Active)
            {
                return Result.Failure<SubscriptionRenewalResponse>(
                    SubscriptionErrors.PlanVersionNotAvailable());
            }

            var openRequest = await store.FindOpenRequestAsync(
                subscription.Id, cancellationToken);

            await unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                if (openRequest is null)
                {
                    var created = await creator.CreateAsync(
                        subscription,
                        planVersion.Id,
                        command.ActorReference,
                        cancellationToken);
                    if (created.IsFailure)
                    {
                        await unitOfWork.RollbackTransactionAsync(cancellationToken);
                        return Result.Failure<SubscriptionRenewalResponse>(created.Error!);
                    }

                    auditWriter.Write(Entry(RequestedAction, created.Value.Id));
                    await unitOfWork.CommitTransactionAsync(cancellationToken);
                    return Result.Success(SubscriptionRenewalResponse.From(created.Value));
                }

                if (openRequest.Status != RenewalRequestStatus.Pending)
                {
                    await unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Failure<SubscriptionRenewalResponse>(
                        SubscriptionErrors.RenewalNotApprovable());
                }

                var now = timeProvider.GetUtcNow();
                if (!openRequest.ChangePlan(
                    planVersion.Id,
                    planVersion.Price,
                    planVersion.CurrencyCode,
                    now))
                {
                    await unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Failure<SubscriptionRenewalResponse>(
                        SubscriptionErrors.RenewalNotApprovable());
                }

                auditWriter.Write(Entry(PlanChangedAction, openRequest.Id));
                await unitOfWork.CommitTransactionAsync(cancellationToken);
                return Result.Success(SubscriptionRenewalResponse.From(openRequest));
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
                Outcome = PlatformAuditOutcome.Succeeded,
            };
    }
}
