using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Identity;
using CafeHub.Application.Plans.Contracts;
using CafeHub.Application.Plans.DTOs;
using CafeHub.Domain.Plans;

namespace CafeHub.Application.Plans.Commands
{
    public sealed class CreatePlanCommandHandler : ICommandHandler<CreatePlanCommand, PlanResponse>
    {
        private const string SucceededAction = "plans.created.succeeded";
        private const string PlanTargetType = "plan";

        private readonly IPlanRepository planRepository;
        private readonly IPlatformAuditWriter auditWriter;
        private readonly ICurrentUser currentUser;
        private readonly TimeProvider timeProvider;

        public CreatePlanCommandHandler(
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

        public async Task<Result<PlanResponse>> Handle(CreatePlanCommand command, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            if (await planRepository.KeyExistsAsync(command.Key, cancellationToken))
            {
                return Result.Failure<PlanResponse>(PlansErrors.KeyAlreadyExists());
            }

            var now = timeProvider.GetUtcNow();
            var plan = Plan.Create(
                Guid.CreateVersion7(),
                command.Key,
                command.Name,
                command.IsSelectable,
                now,
                currentUser.AdminId);

            planRepository.Add(plan);

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
