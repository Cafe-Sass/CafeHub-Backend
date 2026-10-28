using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Onboarding.Contracts;
using CafeHub.Application.Plans.Contracts;
using CafeHub.Domain.Onboarding;

namespace CafeHub.Application.Onboarding.Commands
{
    public sealed class RegisterOwnerCommandHandler : ICommandHandler<RegisterOwnerCommand, OwnerRegistrationResponse>
    {
        private const string SucceededAction = "onboarding.registered.succeeded";
        private const string TargetType = "onboarding_request";

        private readonly IOnboardingRequestStore onboardingRequestStore;
        private readonly ISlugAvailability slugAvailability;
        private readonly IDuplicateRegistrationDetector duplicateRegistrationDetector;
        private readonly IOwnerPasswordHasher ownerPasswordHasher;
        private readonly IPlanRepository planRepository;
        private readonly IMasterUnitOfWork unitOfWork;
        private readonly IPlatformAuditWriter auditWriter;
        private readonly TimeProvider timeProvider;

        public RegisterOwnerCommandHandler(
            IOnboardingRequestStore onboardingRequestStore,
            ISlugAvailability slugAvailability,
            IDuplicateRegistrationDetector duplicateRegistrationDetector,
            IOwnerPasswordHasher ownerPasswordHasher,
            IPlanRepository planRepository,
            IMasterUnitOfWork unitOfWork,
            IPlatformAuditWriter auditWriter,
            TimeProvider timeProvider)
        {
            this.onboardingRequestStore = onboardingRequestStore;
            this.slugAvailability = slugAvailability;
            this.duplicateRegistrationDetector = duplicateRegistrationDetector;
            this.ownerPasswordHasher = ownerPasswordHasher;
            this.planRepository = planRepository;
            this.unitOfWork = unitOfWork;
            this.auditWriter = auditWriter;
            this.timeProvider = timeProvider;
        }

        public async Task<Result<OwnerRegistrationResponse>> Handle(RegisterOwnerCommand command, CancellationToken cancellationToken)
        {
            if (!await planRepository.PlanVersionExistsAsync(command.PlanVersionId, cancellationToken))
            {
                return Result.Failure<OwnerRegistrationResponse>(OnboardingErrors.PlanVersionNotFound());
            }

            TenantSlug slug;
            try
            {
                slug = TenantSlug.Create(command.Slug);
            }
            catch (ArgumentException)
            {
                return Result.Failure<OwnerRegistrationResponse>(OnboardingErrors.SlugInvalid());
            }

            if (!await slugAvailability.IsAvailableAsync(command.Slug, cancellationToken))
            {
                return Result.Failure<OwnerRegistrationResponse>(OnboardingErrors.SlugTaken());
            }

            var emailLower = command.OwnerEmail.ToLowerInvariant();
            var existing = await onboardingRequestStore.FindPublicByEmailAsync(emailLower, cancellationToken);
            if (existing is not null)
            {
                return Result.Success(new OwnerRegistrationResponse(existing.Id, existing.PlanVersionId, existing.Status, true));
            }

            await unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                var now = timeProvider.GetUtcNow();
                var passwordHash = ownerPasswordHasher.Hash(command.Password);
                var request = OnboardingRequest.CreateForPublicRegistration(
                    Guid.CreateVersion7(),
                    command.PlanVersionId,
                    emailLower,
                    command.OwnerFullName,
                    command.OwnerPhone,
                    command.CafeName,
                    passwordHash,
                    slug.Value,
                    now);

                await onboardingRequestStore.AddAsync(request, cancellationToken);

                auditWriter.Write(new PlatformAuditEntry
                {
                    ActorKind = PlatformAuditActorKind.System,
                    Action = SucceededAction,
                    TargetType = TargetType,
                    TargetId = request.Id,
                    Outcome = PlatformAuditOutcome.Succeeded,
                });

                await unitOfWork.CommitTransactionAsync(cancellationToken);

                return Result.Success(new OwnerRegistrationResponse(request.Id, request.PlanVersionId, request.Status, false));
            }
            catch (Exception exception)
            {
                await unitOfWork.RollbackTransactionAsync(cancellationToken);

                if (duplicateRegistrationDetector.IsPublicEmailGuardViolation(exception))
                {
                    var duplicate = await onboardingRequestStore.FindPublicByEmailAsync(emailLower, CancellationToken.None);
                    if (duplicate is not null)
                    {
                        return Result.Success(new OwnerRegistrationResponse(duplicate.Id, duplicate.PlanVersionId, duplicate.Status, true));
                    }
                }

                throw;
            }
        }
    }
}
