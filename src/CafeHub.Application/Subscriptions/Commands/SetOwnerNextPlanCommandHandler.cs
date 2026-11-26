using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Subscriptions.Contracts;
using CafeHub.Application.Subscriptions.DTOs;

namespace CafeHub.Application.Subscriptions.Commands
{
    public sealed class SetOwnerNextPlanCommandHandler(
        ISubscriptionRenewalStore store,
        SubscriptionNextPlanService nextPlan,
        IMasterUnitOfWork unitOfWork,
        IPlatformAuditWriter auditWriter)
        : ICommandHandler<SetOwnerNextPlanCommand, SubscriptionRenewalResponse>
    {
        private const string PlanChangedAction = "subscription.renewal.plan_changed.succeeded";
        private const string RequestedAction = "subscription.renewal.requested.succeeded";
        private const string TargetType = "subscription_renewal_request";

        public async Task<Result<SubscriptionRenewalResponse>> Handle(
            SetOwnerNextPlanCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var subscription = await store.FindSubscriptionByTenantAsync(
                command.TenantKey, cancellationToken);
            if (subscription is null)
            {
                return Result.Failure<SubscriptionRenewalResponse>(SubscriptionErrors.NotFound());
            }

            await unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                var applied = await nextPlan.ApplyAsync(
                    subscription,
                    command.PlanVersionId,
                    command.ActorReference,
                    cancellationToken);
                if (applied.IsFailure)
                {
                    await unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Failure<SubscriptionRenewalResponse>(applied.Error!);
                }

                auditWriter.Write(new PlatformAuditEntry
                {
                    ActorKind = PlatformAuditActorKind.System,
                    Action = applied.Value.Created ? RequestedAction : PlanChangedAction,
                    TargetType = TargetType,
                    TargetId = applied.Value.Request.Id,
                    Outcome = PlatformAuditOutcome.Succeeded,
                });
                await unitOfWork.CommitTransactionAsync(cancellationToken);
                return Result.Success(SubscriptionRenewalResponse.From(applied.Value.Request));
            }
            catch (Exception)
            {
                await unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw;
            }
        }
    }
}
