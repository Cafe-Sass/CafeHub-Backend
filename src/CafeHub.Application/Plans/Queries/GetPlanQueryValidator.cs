using FluentValidation;

namespace CafeHub.Application.Plans.Queries
{
    public sealed class GetPlanQueryValidator : AbstractValidator<GetPlanQuery>
    {
        public GetPlanQueryValidator()
        {
            RuleFor(query => query.PlanId)
                .NotEmpty();
        }
    }
}
