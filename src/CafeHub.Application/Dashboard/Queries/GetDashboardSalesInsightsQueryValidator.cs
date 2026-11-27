using FluentValidation;

namespace CafeHub.Application.Dashboard.Queries
{
    public sealed class GetDashboardSalesInsightsQueryValidator : AbstractValidator<GetDashboardSalesInsightsQuery>
    {
        public GetDashboardSalesInsightsQueryValidator()
        {
            RuleFor(query => query.From)
                .NotEqual(default(DateOnly))
                .WithMessage("The range start is required.");

            RuleFor(query => query.To)
                .NotEqual(default(DateOnly))
                .WithMessage("The range end is required.");

            RuleFor(query => query.To)
                .Must((query, to) => to > query.From)
                .WithMessage("The range end must be after the range start.")
                .When(query => query.From != default && query.To != default);

            RuleFor(query => query.To)
                .Must((query, to) => to.DayNumber - query.From.DayNumber <= DashboardDateRange.MaximumDays)
                .WithMessage($"The range cannot exceed {DashboardDateRange.MaximumDays} days.")
                .When(query => query.From != default && query.To != default && query.To > query.From);

            RuleFor(query => query.BranchId)
                .Must(branchId => branchId is null || branchId != Guid.Empty)
                .WithMessage("The branch id cannot be empty.");
        }
    }
}
