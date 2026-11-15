using FluentValidation;

namespace CafeHub.Application.Stations.Commands
{
    public sealed class UpdateStationCommandValidator : AbstractValidator<UpdateStationCommand>
    {
        public UpdateStationCommandValidator()
        {
            RuleFor(command => command.StationId)
                .NotEmpty();

            RuleFor(command => command.Name)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .Must(name => name.Trim().Length <= CreateStationCommandValidator.MaximumNameLength)
                .WithMessage("The station name must be between 1 and 64 characters after trimming.")
                .Must(name => !name.Any(char.IsControl))
                .WithMessage("The station name must not contain control characters.");

            RuleFor(command => command.SortOrder)
                .GreaterThanOrEqualTo(0);

            RuleFor(command => command.ConcurrencyToken)
                .NotEmpty()
                .Must(BeValidConcurrencyToken)
                .WithMessage("The concurrency token is not a valid unsigned integer.");
        }

        private static bool BeValidConcurrencyToken(string? token)
        {
            return uint.TryParse(token, out _);
        }
    }
}
