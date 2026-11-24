using CafeHub.Domain.Tenants;
using FluentValidation;

namespace CafeHub.Application.Wifi.Commands
{
    public sealed class UpdateWifiSettingsCommandValidator : AbstractValidator<UpdateWifiSettingsCommand>
    {
        public UpdateWifiSettingsCommandValidator()
        {
            RuleFor(command => command.Policy)
                .Must(policy => Enum.IsDefined(policy))
                .WithMessage("The WiFi policy must be Free or Paid.");

            RuleFor(command => command.ConcurrencyToken)
                .NotNull()
                .WithMessage("The concurrency token is required; it is empty for the default policy.");
        }
    }
}
