using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Identity;
using CafeHub.Application.Onboarding.Contracts;
using CafeHub.Application.Onboarding.DTOs;
using CafeHub.Domain.Onboarding;

namespace CafeHub.Application.Onboarding.Commands
{
    public sealed class RejectOnboardingRequestCommandHandler
        : ICommandHandler<RejectOnboardingRequestCommand, RejectionResponse>
    {
        private const string SucceededAction = "onboarding.rejected.succeeded";
        private const string TargetType = "onboarding_request";

        private readonly IOnboardingRequestStore onboardingRequestStore;
        private readonly IMasterUnitOfWork unitOfWork;
        private readonly IPlatformAuditWriter auditWriter;
        private readonly ICurrentUser currentUser;
        private readonly TimeProvider timeProvider;

        public RejectOnboardingRequestCommandHandler(
            IOnboardingRequestStore onboardingRequestStore,
            IMasterUnitOfWork unitOfWork,
            IPlatformAuditWriter auditWriter,
            ICurrentUser currentUser,
            TimeProvider timeProvider)
        {
            this.onboardingRequestStore = onboardingRequestStore ?? throw new ArgumentNullException(nameof(onboardingRequestStore));
            this.unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            this.auditWriter = auditWriter ?? throw new ArgumentNullException(nameof(auditWriter));
            this.currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        }

        public async Task<Result<RejectionResponse>> Handle(
            RejectOnboardingRequestCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var now = timeProvider.GetUtcNow();

            var request = await onboardingRequestStore.FindByIdAsync(command.RequestId, cancellationToken);
            if (request is null)
            {
                return Result.Failure<RejectionResponse>(OnboardingErrors.RequestNotFound());
            }

            // Already rejected: idempotent, return the same response.
            if (request.Status == OnboardingRequestStatus.Rejected)
            {
                return Result.Success(new RejectionResponse(command.RequestId, request.Status.ToString()));
            }

            // Not ready for review (or already approved): cannot reject.
            if (request.Status != OnboardingRequestStatus.PaidAwaitingReview)
            {
                return Result.Failure<RejectionResponse>(OnboardingErrors.RequestNotRejectable());
            }

            var reviewerId = currentUser.AdminId ?? throw new InvalidOperationException("The reviewing administrator is required.");

            var reason = command.Reason?.Trim();

            await unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                request.Reject(reviewerId, now, reason);

                await onboardingRequestStore.UpdateAsync(request, cancellationToken);

                auditWriter.Write(new PlatformAuditEntry
                {
                    ActorKind = PlatformAuditActorKind.Admin,
                    Action = SucceededAction,
                    TargetType = TargetType,
                    TargetId = command.RequestId,
                    Outcome = PlatformAuditOutcome.Succeeded,
                });

                await unitOfWork.CommitTransactionAsync(cancellationToken);

                return Result.Success(new RejectionResponse(command.RequestId, request.Status.ToString()));
            }
            catch (Exception)
            {
                await unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw;
            }
        }
    }
}
