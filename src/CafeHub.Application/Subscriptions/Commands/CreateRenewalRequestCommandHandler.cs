using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Subscriptions.Contracts;
using CafeHub.Application.Subscriptions.DTOs;

namespace CafeHub.Application.Subscriptions.Commands
{
    public sealed class CreateRenewalRequestCommandHandler(
        ISubscriptionRenewalStore store,
        SubscriptionRenewalCreator creator,
        IMasterUnitOfWork unitOfWork,
        IPlatformAuditWriter auditWriter)
        : ICommandHandler<CreateRenewalRequestCommand, SubscriptionRenewalResponse>
    {
        private const string RequestedAction = "subscription.renewal.requested.succeeded";
        private const string TargetType = "subscription_renewal_request";

        public async Task<Result<SubscriptionRenewalResponse>> Handle(
            CreateRenewalRequestCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var subscription = await store.FindSubscriptionAsync(
                command.SubscriptionId, cancellationToken);
            if (subscription is null)
            {
                return Result.Failure<SubscriptionRenewalResponse>(SubscriptionErrors.NotFound());
            }

            var planVersionId = command.PlanVersionId;
            if (planVersionId is null)
            {
                var latestPeriod = await store.FindLatestPeriodAsync(
                    subscription.Id, cancellationToken);
                if (latestPeriod is null)
                {
                    return Result.Failure<SubscriptionRenewalResponse>(
                        SubscriptionErrors.PlanVersionNotAvailable());
                }

                planVersionId = latestPeriod.PlanVersionId;
            }

            await unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                var created = await creator.CreateAsync(
                    subscription, planVersionId.Value, command.ActorReference, cancellationToken);
                if (created.IsFailure)
                {
                    await unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Failure<SubscriptionRenewalResponse>(created.Error!);
                }

                auditWriter.Write(new PlatformAuditEntry
                {
                    ActorKind = PlatformAuditActorKind.System,
                    Action = RequestedAction,
                    TargetType = TargetType,
                    TargetId = created.Value.Id,
                    Outcome = PlatformAuditOutcome.Succeeded,
                });

                await unitOfWork.CommitTransactionAsync(cancellationToken);
                return Result.Success(SubscriptionRenewalResponse.From(created.Value));
            }
            catch (Exception)
            {
                await unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw;
            }
        }
    }
}
