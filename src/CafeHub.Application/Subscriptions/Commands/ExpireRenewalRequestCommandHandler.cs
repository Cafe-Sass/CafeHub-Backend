using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Subscriptions.Contracts;
using CafeHub.Application.Subscriptions.DTOs;
using CafeHub.Domain.Subscriptions;

namespace CafeHub.Application.Subscriptions.Commands
{
    public sealed class ExpireRenewalRequestCommandHandler(
        ISubscriptionRenewalStore store,
        IMasterUnitOfWork unitOfWork,
        IPlatformAuditWriter auditWriter,
        TimeProvider timeProvider)
        : ICommandHandler<ExpireRenewalRequestCommand, SubscriptionRenewalResponse>
    {
        private const string ExpiredAction = "subscription.renewal.expired.succeeded";
        private const string TargetType = "subscription_renewal_request";

        public async Task<Result<SubscriptionRenewalResponse>> Handle(
            ExpireRenewalRequestCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var request = await store.FindRequestAsync(command.RenewalRequestId, cancellationToken);
            if (request is null)
            {
                return Result.Failure<SubscriptionRenewalResponse>(
                    SubscriptionErrors.RenewalRequestNotFound());
            }

            var now = timeProvider.GetUtcNow();
            if (request.Status == RenewalRequestStatus.Expired)
            {
                return Result.Success(SubscriptionRenewalResponse.From(request));
            }

            if (request.Status != RenewalRequestStatus.Pending || !request.IsExpired(now))
            {
                return Result.Failure<SubscriptionRenewalResponse>(
                    SubscriptionErrors.RenewalNotApprovable());
            }

            await unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                request.Expire(now);
                auditWriter.Write(new PlatformAuditEntry
                {
                    ActorKind = PlatformAuditActorKind.System,
                    Action = ExpiredAction,
                    TargetType = TargetType,
                    TargetId = request.Id,
                    Outcome = PlatformAuditOutcome.Succeeded,
                });
                await unitOfWork.CommitTransactionAsync(cancellationToken);
                return Result.Success(SubscriptionRenewalResponse.From(request));
            }
            catch (Exception)
            {
                await unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw;
            }
        }
    }
}
