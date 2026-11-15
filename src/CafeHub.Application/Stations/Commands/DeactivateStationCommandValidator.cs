using FluentValidation;

namespace CafeHub.Application.Stations.Commands
{
    public sealed class DeactivateStationCommandValidator : AbstractValidator<DeactivateStationCommand>
    {
        public DeactivateStationCommandValidator()
        {
            RuleFor(command => command.StationId)
                .NotEmpty();

            RuleFor(command => command.ConcurrencyToken)
                .NotEmpty()
                .Must(token => uint.TryParse(token, out _))
                .WithMessage("The concurrency token is not a valid unsigned integer.");
        }
    }
}
