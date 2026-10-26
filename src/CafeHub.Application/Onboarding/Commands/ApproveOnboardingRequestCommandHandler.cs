using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Identity;
using CafeHub.Application.Onboarding.Contracts;
using CafeHub.Application.Onboarding.DTOs;
using CafeHub.Domain.Onboarding;
using CafeHub.Domain.Provisioning;
using CafeHub.Domain.Subscriptions;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Onboarding.Commands
{
    public sealed class ApproveOnboardingRequestCommandHandler
        : ICommandHandler<ApproveOnboardingRequestCommand, ApprovalResponse>
    {
        private const string SucceededAction = "onboarding.approved.succeeded";
        private const string TargetType = "onboarding_request";

        private readonly IOnboardingRequestStore onboardingRequestStore;
        private readonly ITenantStore tenantStore;
        private readonly ISubscriptionRepository subscriptionRepository;
        private readonly IProvisioningJobStore provisioningJobStore;
        private readonly IMasterUnitOfWork unitOfWork;
        private readonly IPlatformAuditWriter auditWriter;
        private readonly ICurrentUser currentUser;
        private readonly TimeProvider timeProvider;

        public ApproveOnboardingRequestCommandHandler(
            IOnboardingRequestStore onboardingRequestStore,
            ITenantStore tenantStore,
            ISubscriptionRepository subscriptionRepository,
            IProvisioningJobStore provisioningJobStore,
            IMasterUnitOfWork unitOfWork,
            IPlatformAuditWriter auditWriter,
            ICurrentUser currentUser,
            TimeProvider timeProvider)
        {
            this.onboardingRequestStore = onboardingRequestStore ?? throw new ArgumentNullException(nameof(onboardingRequestStore));
            this.tenantStore = tenantStore ?? throw new ArgumentNullException(nameof(tenantStore));
            this.subscriptionRepository = subscriptionRepository ?? throw new ArgumentNullException(nameof(subscriptionRepository));
            this.provisioningJobStore = provisioningJobStore ?? throw new ArgumentNullException(nameof(provisioningJobStore));
            this.unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            this.auditWriter = auditWriter ?? throw new ArgumentNullException(nameof(auditWriter));
            this.currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        }

        public async Task<Result<ApprovalResponse>> Handle(
            ApproveOnboardingRequestCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var now = timeProvider.GetUtcNow();

            var request = await onboardingRequestStore.FindByIdAsync(command.RequestId, cancellationToken);
            if (request is null)
            {
                return Result.Failure<ApprovalResponse>(OnboardingErrors.RequestNotFound());
            }

            // Already approved by anyone: idempotent, return the same tenant id.
            if (request.Status == OnboardingRequestStatus.Approved)
            {
                var existingTenant = await tenantStore.FindByOnboardingRequestIdAsync(command.RequestId, cancellationToken);
                return Result.Success(new ApprovalResponse(
                    command.RequestId,
                    existingTenant!.Id,
                    request.Status.ToString()));
            }

            // Not ready for review (or already rejected/cancelled): cannot approve.
            if (request.Status != OnboardingRequestStatus.PaidAwaitingReview)
            {
                return Result.Failure<ApprovalResponse>(OnboardingErrors.RequestNotApprovable());
            }

            var reviewerId = currentUser.AdminId ?? throw new InvalidOperationException("The reviewing administrator is required.");

            await unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                // Create the tenant row from the request.
                var tenantId = Guid.CreateVersion7();
                var tenant = Tenant.Create(
                    tenantId,
                    command.RequestId,
                    request.TenantSlug,
                    TenantLifecycleStatus.Provisioning,
                    now,
                    reviewerId);

                await tenantStore.AddAsync(tenant, cancellationToken);

                // Link the existing subscription to the tenant.
                var subscription = await subscriptionRepository.FindByOnboardingRequestIdAsync(
                    command.RequestId,
                    cancellationToken);

                if (subscription is null)
                {
                    await unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Failure<ApprovalResponse>(OnboardingErrors.RequestNotApprovable());
                }

                subscription.LinkToTenant(tenantId, now);

                // Create one pending provisioning job.
                var jobId = Guid.CreateVersion7();
                var job = ProvisioningJob.Create(jobId, tenantId, ProvisioningJobStatus.Pending, now, reviewerId);

                await provisioningJobStore.AddAsync(job, cancellationToken);

                // Set the request to Approved with reviewer and time.
                request.Approve(reviewerId, now);

                await onboardingRequestStore.UpdateAsync(request, cancellationToken);

                // Write the audit entry.
                auditWriter.Write(new PlatformAuditEntry
                {
                    ActorKind = PlatformAuditActorKind.Admin,
                    Action = SucceededAction,
                    TargetType = TargetType,
                    TargetId = command.RequestId,
                    Outcome = PlatformAuditOutcome.Succeeded,
                });

                await unitOfWork.CommitTransactionAsync(cancellationToken);

                return Result.Success(new ApprovalResponse(command.RequestId, tenantId, request.Status.ToString()));
            }
            catch (Exception)
            {
                await unitOfWork.RollbackTransactionAsync(cancellationToken);

                // Concurrency: unique constraints (tenant slug, job key, one job per tenant)
                // resolve simultaneous approvals. Re-read and return the idempotent response.
                var existingRequest = await onboardingRequestStore.FindByIdAsync(command.RequestId, cancellationToken);
                if (existingRequest is not null && existingRequest.Status == OnboardingRequestStatus.Approved)
                {
                    var existingTenant = await tenantStore.FindByOnboardingRequestIdAsync(command.RequestId, cancellationToken);
                    return Result.Success(new ApprovalResponse(
                        command.RequestId,
                        existingTenant!.Id,
                        existingRequest.Status.ToString()));
                }

                throw;
            }
        }
    }
}
