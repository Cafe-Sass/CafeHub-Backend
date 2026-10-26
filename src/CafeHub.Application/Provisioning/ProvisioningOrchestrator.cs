using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Identity;
using CafeHub.Application.Onboarding.Contracts;
using CafeHub.Application.Provisioning;
using CafeHub.Application.Provisioning.Contracts;
using CafeHub.Domain.Onboarding;
using CafeHub.Domain.Provisioning;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Provisioning
{
    public sealed class ProvisioningOrchestrator : IProvisioningJobRunner
    {
        private const string EnsureDatabaseStep = "EnsureDatabase";
        private const string InitializeSchemaStep = "InitializeSchema";
        private const string MarkReadyStep = "MarkReady";

        private static readonly IReadOnlyList<string> Steps = new[]
        {
            EnsureDatabaseStep,
            InitializeSchemaStep,
            MarkReadyStep,
        };

        private readonly IProvisioningJobStore jobStore;
        private readonly IProvisioningJobAttemptStore attemptStore;
        private readonly ITenantStore tenantStore;
        private readonly ITenantDatabaseStore tenantDatabaseStore;
        private readonly IOnboardingRequestStore onboardingRequestStore;
        private readonly ITenantDatabaseCreator databaseCreator;
        private readonly ITenantSchemaInitializer schemaInitializer;
        private readonly IMasterUnitOfWork unitOfWork;
        private readonly IPlatformAuditWriter auditWriter;
        private readonly ICurrentUser currentUser;
        private readonly TimeProvider timeProvider;
        private readonly ProvisioningRetryOptions retryOptions;

        public ProvisioningOrchestrator(
            IProvisioningJobStore jobStore,
            IProvisioningJobAttemptStore attemptStore,
            ITenantStore tenantStore,
            ITenantDatabaseStore tenantDatabaseStore,
            IOnboardingRequestStore onboardingRequestStore,
            ITenantDatabaseCreator databaseCreator,
            ITenantSchemaInitializer schemaInitializer,
            IMasterUnitOfWork unitOfWork,
            IPlatformAuditWriter auditWriter,
            ICurrentUser currentUser,
            TimeProvider timeProvider,
            ProvisioningRetryOptions retryOptions)
        {
            this.jobStore = jobStore;
            this.attemptStore = attemptStore;
            this.tenantStore = tenantStore;
            this.tenantDatabaseStore = tenantDatabaseStore;
            this.onboardingRequestStore = onboardingRequestStore;
            this.databaseCreator = databaseCreator;
            this.schemaInitializer = schemaInitializer;
            this.unitOfWork = unitOfWork;
            this.auditWriter = auditWriter;
            this.currentUser = currentUser;
            this.timeProvider = timeProvider;
            this.retryOptions = retryOptions;
        }

        public async Task RunAsync(ClaimedProvisioningJob claimedJob, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(claimedJob);

            var now = timeProvider.GetUtcNow();
            var job = await jobStore.FindByIdAsync(claimedJob.JobId, cancellationToken);

            if (job is null)
            {
                return;
            }

            var tenant = await tenantStore.FindByIdAsync(job.TenantId, cancellationToken);
            if (tenant is null)
            {
                return;
            }

            var request = await onboardingRequestStore.FindByIdAsync(
                tenant.OnboardingRequestId, cancellationToken);

            if (tenant.LifecycleStatus == TenantLifecycleStatus.Active)
            {
                await CompleteJobAsync(job, now, cancellationToken);
                return;
            }

            var attemptCount = (await attemptStore.FindByJobIdAsync(job.Id, cancellationToken)).Count;

            for (var index = 0; index < Steps.Count; index++)
            {
                if (!leaseStillValid(job, now))
                {
                    return;
                }

                var step = Steps[index];
                var stepNow = timeProvider.GetUtcNow();
                var attempt = ProvisioningJobAttempt.Create(
                    Guid.CreateVersion7(),
                    job.Id,
                    attemptCount + 1,
                    ProvisioningJobAttemptStatus.Running,
                    stepNow,
                    null,
                    null,
                    stepNow);

                await attemptStore.AddAsync(attempt, cancellationToken);

                var stepResult = await RunStepAsync(index, tenant, request, cancellationToken);

                var finishedAt = timeProvider.GetUtcNow();
                if (stepResult.IsSuccess)
                {
                    attempt.Complete(finishedAt);
                    attemptCount++;

                    if (index == Steps.Count - 1)
                    {
                        await CompleteJobAsync(job, finishedAt, cancellationToken);
                        await MarkTenantReadyAsync(tenant, finishedAt, cancellationToken);
                        return;
                    }
                }
                else
                {
                    attempt.Fail(finishedAt, SanitizeError(stepResult.Error!.Message));
                    attemptCount++;

                    var nextAttempt = stepNow.AddSeconds(BackoffSeconds(attemptCount));
                    await FailOrRescheduleJobAsync(job, attemptCount, nextAttempt, cancellationToken);
                    await WriteFailureAuditAsync(job, stepResult.Error, cancellationToken);
                    return;
                }
            }
        }

        private async Task<Result> RunStepAsync(int index, Tenant tenant, OnboardingRequest? request, CancellationToken cancellationToken)
        {
            return index switch
            {
                0 => await EnsureDatabaseAsync(tenant),
                1 => await InitializeSchemaAsync(tenant, request),
                _ => await MarkReadyStepResultAsync(),
            };
        }

        private async Task<Result> EnsureDatabaseAsync(Tenant tenant)
        {
            var result = await databaseCreator.EnsureDatabaseAsync(
                new TenantDatabaseSpec(tenant.Id, tenant.Slug),
                CancellationToken.None);
            return result.IsSuccess ? Result.Success() : Result.Failure(result.Error!);
        }

        private async Task<Result> InitializeSchemaAsync(Tenant tenant, OnboardingRequest? request)
        {
            if (request is null)
            {
                return Result.Failure(ProvisioningErrors.InvalidTenantId());
            }

            return await schemaInitializer.InitializeAsync(
                new TenantInitializationRequest(
                    tenant.Id,
                    tenant.Slug,
                    request.OwnerEmail,
                    request.OwnerFullName,
                    request.OwnerPhoneNumber,
                    request.OwnerPasswordHash ?? string.Empty,
                    request.CafeName,
                    request.PlanVersionId.ToString()),
                CancellationToken.None);
        }

        private Task<Result> MarkReadyStepResultAsync()
        {
            return Task.FromResult(Result.Success());
        }

        private async Task CompleteJobAsync(ProvisioningJob job, DateTimeOffset now, CancellationToken cancellationToken)
        {
            job.Complete(now);
            await jobStore.UpdateAsync(job, cancellationToken);
        }

        private async Task MarkTenantReadyAsync(Tenant tenant, DateTimeOffset now, CancellationToken cancellationToken)
        {
            await unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                tenant.TransitionTo(TenantLifecycleStatus.Active, now);
                await unitOfWork.CommitTransactionAsync(cancellationToken);

                auditWriter.Write(new PlatformAuditEntry
                {
                    ActorKind = PlatformAuditActorKind.System,
                    Action = "tenant.provisioned.succeeded",
                    TargetType = "tenant",
                    TargetId = tenant.Id,
                    Outcome = PlatformAuditOutcome.Succeeded,
                });
            }
            catch
            {
                await unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw;
            }
        }

        private async Task FailOrRescheduleJobAsync(
            ProvisioningJob job,
            int attemptCount,
            DateTimeOffset nextAttempt,
            CancellationToken cancellationToken)
        {
            if (attemptCount >= retryOptions.MaxAttempts)
            {
                job.Fail(timeProvider.GetUtcNow());
                await jobStore.UpdateAsync(job, cancellationToken);
            }
            else
            {
                job.Reschedule(timeProvider.GetUtcNow(), nextAttempt);
                await jobStore.UpdateAsync(job, cancellationToken);
            }
        }

        private async Task WriteFailureAuditAsync(
            ProvisioningJob job,
            Error error,
            CancellationToken cancellationToken)
        {
            auditWriter.Write(new PlatformAuditEntry
            {
                ActorKind = PlatformAuditActorKind.System,
                Action = "tenant.provisioning.failed",
                TargetType = "tenant",
                TargetId = job.TenantId,
                Outcome = PlatformAuditOutcome.Failed,
                Reason = error.Code,
            });
        }

        private static bool leaseStillValid(ProvisioningJob job, DateTimeOffset now)
        {
            return job.LeaseExpiresAt is not null && job.LeaseExpiresAt > now;
        }

        private int BackoffSeconds(int attemptCount)
        {
            var seconds = retryOptions.BaseSeconds;
            for (var i = 1; i < attemptCount; i++)
            {
                seconds *= 2;
            }

            return Math.Min(seconds, retryOptions.MaxSeconds);
        }

        private static string SanitizeError(string? message)
        {
            return SanitizedFailure.Create(message).Value;
        }
    }
}
