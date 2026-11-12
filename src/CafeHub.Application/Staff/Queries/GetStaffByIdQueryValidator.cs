using FluentValidation;

namespace CafeHub.Application.Staff.Queries
{
    public sealed class GetStaffByIdQueryValidator : AbstractValidator<GetStaffByIdQuery>
    {
        public GetStaffByIdQueryValidator()
        {
            RuleFor(query => query.StaffId)
                .NotEmpty()
                .WithMessage("The staff id is required.");
        }
    }
}
