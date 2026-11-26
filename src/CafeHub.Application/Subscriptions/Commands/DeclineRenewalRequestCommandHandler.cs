using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Subscriptions.Contracts;
using CafeHub.Application.Subscriptions.DTOs;
using CafeHub.Domain.Subscriptions;

namespace CafeHub.Application.Subscriptions.Commands
{
    public sealed class DeclineRenewalRequestCommandHandler(
        ISubscriptionRenewalStore store,
        IMasterUnitOfWork unitOfWork,
        IPlatformAuditWriter auditWriter,
        TimeProvider timeProvider)
        : ICommandHandler<DeclineRenewalRequestCommand, SubscriptionRenewalResponse>
    {
        private const string DeclinedAction = "subscription.renewal.declined.succeeded";
        private const string ExpiredAction = "subscription.renewal.expired.succeeded";
        private const string TargetType = "subscription_renewal_request";

        public async Task<Result<SubscriptionRenewalResponse>> Handle(
            DeclineRenewalRequestCommand command,
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
            if (subscription is null || subscription.TenantId != command.TenantKey)
            {
                return Result.Failure<SubscriptionRenewalResponse>(
                    SubscriptionErrors.RenewalRequestNotFound());
            }

            if (request.Status == RenewalRequestStatus.Declined)
            {
                return Result.Success(SubscriptionRenewalResponse.From(request));
            }

            if (request.Status == RenewalRequestStatus.Expired)
            {
                return Result.Failure<SubscriptionRenewalResponse>(
                    SubscriptionErrors.RenewalExpired());
            }

            if (request.Status != RenewalRequestStatus.Pending)
            {
                return Result.Failure<SubscriptionRenewalResponse>(
                    SubscriptionErrors.RenewalNotApprovable());
            }

            var now = timeProvider.GetUtcNow();
            await unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                if (request.IsExpired(now))
                {
                    request.Expire(now);
                    auditWriter.Write(Entry(ExpiredAction, request.Id));
                    await unitOfWork.CommitTransactionAsync(cancellationToken);
                    return Result.Failure<SubscriptionRenewalResponse>(
                        SubscriptionErrors.RenewalExpired());
                }

                if (!request.Decline(now, command.ActorReference))
                {
                    await unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Failure<SubscriptionRenewalResponse>(
                        SubscriptionErrors.RenewalNotApprovable());
                }

                auditWriter.Write(Entry(DeclinedAction, request.Id));
                await unitOfWork.CommitTransactionAsync(cancellationToken);
                return Result.Success(SubscriptionRenewalResponse.From(request));
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
