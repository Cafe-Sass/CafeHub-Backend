using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Plans.Contracts;
using CafeHub.Application.Plans.DTOs;

namespace CafeHub.Application.Plans.Queries
{
    public sealed class GetPlanQueryHandler : IQueryHandler<GetPlanQuery, PlanResponse>
    {
        private readonly IPlanRepository planRepository;

        public GetPlanQueryHandler(IPlanRepository planRepository)
        {
            this.planRepository = planRepository ?? throw new ArgumentNullException(nameof(planRepository));
        }

        public async Task<Result<PlanResponse>> Handle(GetPlanQuery query, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var plan = await planRepository.FindByIdWithVersionsAsync(query.PlanId, cancellationToken);
            if (plan is null)
            {
                return Result.Failure<PlanResponse>(PlansErrors.NotFound());
            }

            return Result.Success(PlanResponse.From(plan));
        }
    }
}
