using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Identity;
using CafeHub.Application.Plans.Contracts;
using CafeHub.Application.Plans.DTOs;
using CafeHub.Domain.Plans;

namespace CafeHub.Application.Plans.Commands
{
    public sealed class CreatePlanVersionCommandHandler : ICommandHandler<CreatePlanVersionCommand, PlanResponse>
    {
        private const string SucceededAction = "plans.version_created.succeeded";
        private const string PlanTargetType = "plan";

        private readonly IPlanRepository planRepository;
        private readonly IPlatformAuditWriter auditWriter;
        private readonly ICurrentUser currentUser;
        private readonly TimeProvider timeProvider;

        public CreatePlanVersionCommandHandler(
            IPlanRepository planRepository,
            IPlatformAuditWriter auditWriter,
            ICurrentUser currentUser,
            TimeProvider timeProvider)
        {
            this.planRepository = planRepository ?? throw new ArgumentNullException(nameof(planRepository));
            this.auditWriter = auditWriter ?? throw new ArgumentNullException(nameof(auditWriter));
            this.currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        }

        public async Task<Result<PlanResponse>> Handle(CreatePlanVersionCommand command, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var plan = await planRepository.FindByIdWithVersionsAsync(command.PlanId, cancellationToken);
            if (plan is null)
            {
                return Result.Failure<PlanResponse>(PlansErrors.NotFound());
            }

            var now = timeProvider.GetUtcNow();
            var versionId = Guid.CreateVersion7();
            var entitlements = command.Entitlements
                .Select(entitlement => PlanEntitlement.Create(
                    Guid.CreateVersion7(),
                    versionId,
                    entitlement.Key,
                    entitlement.Value,
                    now))
                .ToArray();

            plan.AddVersion(
                versionId,
                command.Price,
                command.Currency,
                Enum.Parse<BillingPeriod>(command.Period, ignoreCase: false),
                PlanVersionStatus.Active,
                entitlements,
                now,
                currentUser.AdminId);

            auditWriter.Write(new PlatformAuditEntry
            {
                ActorKind = PlatformAuditActorKind.Admin,
                Action = SucceededAction,
                TargetType = PlanTargetType,
                TargetId = plan.Id,
                Outcome = PlatformAuditOutcome.Succeeded,
            });

            return Result.Success(PlanResponse.From(plan));
        }
    }
}
