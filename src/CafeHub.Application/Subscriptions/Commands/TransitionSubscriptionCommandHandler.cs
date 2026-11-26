using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Identity;
using CafeHub.Application.Subscriptions.Contracts;
using CafeHub.Application.Subscriptions.DTOs;
using CafeHub.Domain.Subscriptions;

namespace CafeHub.Application.Subscriptions.Commands
{
    public sealed class TransitionSubscriptionCommandHandler(
        ISubscriptionLifecycleStore store,
        IMasterUnitOfWork unitOfWork,
        IPlatformAuditWriter auditWriter,
        ICurrentUser currentUser,
        TimeProvider timeProvider)
        : ICommandHandler<TransitionSubscriptionCommand, SubscriptionLifecycleResponse>
    {
        private const string SucceededAction = "subscription.lifecycle.succeeded";
        private const string TargetType = "subscription";

        public async Task<Result<SubscriptionLifecycleResponse>> Handle(
            TransitionSubscriptionCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var subscription = await store.FindAsync(command.SubscriptionId, cancellationToken);
            if (subscription is null)
            {
                return Result.Failure<SubscriptionLifecycleResponse>(SubscriptionErrors.NotFound());
            }

            if (!subscription.CanTransitionTo(command.TargetStatus))
            {
                return Result.Failure<SubscriptionLifecycleResponse>(
                    SubscriptionErrors.InvalidTransition());
            }

            var now = timeProvider.GetUtcNow();
            var previousStatus = subscription.Status;
            var period = await store.FindLatestPeriodAsync(
                command.SubscriptionId,
                cancellationToken);

            await unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                if (!subscription.TransitionTo(command.TargetStatus, now, currentUser.AdminId))
                {
                    await unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Failure<SubscriptionLifecycleResponse>(
                        SubscriptionErrors.InvalidTransition());
                }

                store.AddHistory(SubscriptionHistory.Create(
                    Guid.CreateVersion7(),
                    subscription.Id,
                    previousStatus,
                    subscription.Status,
                    period?.PlanVersionId,
                    period?.PeriodNumber,
                    currentUser.AdminId,
                    null,
                    command.Reason,
                    now));

                auditWriter.Write(new PlatformAuditEntry
                {
                    ActorKind = currentUser.AdminId is null
                        ? PlatformAuditActorKind.System
                        : PlatformAuditActorKind.Admin,
                    Action = SucceededAction,
                    TargetType = TargetType,
                    TargetId = subscription.Id,
                    Outcome = PlatformAuditOutcome.Succeeded,
                });

                await unitOfWork.CommitTransactionAsync(cancellationToken);

                return Result.Success(new SubscriptionLifecycleResponse(
                    subscription.Id,
                    previousStatus.ToString(),
                    subscription.Status.ToString(),
                    now));
            }
            catch (Exception)
            {
                await unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw;
            }
        }
    }
}
