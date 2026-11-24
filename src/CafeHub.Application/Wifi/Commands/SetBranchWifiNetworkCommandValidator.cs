using CafeHub.Domain.Tenants;
using FluentValidation;

namespace CafeHub.Application.Wifi.Commands
{
    public sealed class SetBranchWifiNetworkCommandValidator : AbstractValidator<SetBranchWifiNetworkCommand>
    {
        public SetBranchWifiNetworkCommandValidator()
        {
            RuleFor(command => command.BranchId)
                .NotEmpty()
                .WithMessage("The branch id is required.");

            RuleFor(command => command.Ssid)
                .Must(ssid => string.IsNullOrWhiteSpace(ssid) || BranchWifiNetwork.IsValidSsid(ssid))
                .WithMessage(
                    $"The WiFi network name must be 1 to {BranchWifiNetwork.MaximumSsidLength} characters without control characters.");

            RuleFor(command => command.Security)
                .Must(security => security is null || Enum.IsDefined(security.Value))
                .WithMessage("The WiFi security type must be None, WPA, or WEP.");

            RuleFor(command => command)
                .Custom((command, context) =>
                {
                    if (!string.IsNullOrWhiteSpace(command.Ssid) && command.Security is null)
                    {
                        context.AddFailure(
                            nameof(SetBranchWifiNetworkCommand.Security),
                            "The WiFi security type is required when a network name is set.");
                    }
                });

            RuleFor(command => command.Password)
                .Must(BranchWifiNetwork.IsValidPassword)
                .WithMessage(
                    $"The WiFi password must be empty or 1 to {BranchWifiNetwork.MaximumPasswordLength} characters without control characters.");
        }
    }
}
