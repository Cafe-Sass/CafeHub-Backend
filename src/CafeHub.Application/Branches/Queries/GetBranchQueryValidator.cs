using FluentValidation;

namespace CafeHub.Application.Branches.Queries
{
    public sealed class GetBranchQueryValidator : AbstractValidator<GetBranchQuery>
    {
        public GetBranchQueryValidator()
        {
            RuleFor(query => query.BranchId)
                .NotEmpty();
        }
    }
}
