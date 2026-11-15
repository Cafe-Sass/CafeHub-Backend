using FluentValidation;

namespace CafeHub.Application.Stations.Commands
{
    public sealed class CreateStationCommandValidator : AbstractValidator<CreateStationCommand>
    {
        public const int MaximumNameLength = 64;

        public CreateStationCommandValidator()
        {
            RuleFor(command => command.BranchId)
                .NotEmpty();

            RuleFor(command => command.Name)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .Must(name => name.Trim().Length <= MaximumNameLength)
                .WithMessage("The station name must be between 1 and 64 characters after trimming.")
                .Must(name => !name.Any(char.IsControl))
                .WithMessage("The station name must not contain control characters.");

            RuleFor(command => command.SortOrder)
                .GreaterThanOrEqualTo(0);
        }
    }
}
