using FluentValidation;

namespace CafeHub.Application.Stations.Commands
{
    public sealed class SetStaffStationsCommandValidator : AbstractValidator<SetStaffStationsCommand>
    {
        public SetStaffStationsCommandValidator()
        {
            RuleFor(command => command.StaffId)
                .NotEmpty();

            RuleFor(command => command.StationIds)
                .NotNull()
                .Must(ids => ids.Distinct().Count() == ids.Count)
                .WithMessage("Duplicate station ids are not allowed.");
        }
    }
}
