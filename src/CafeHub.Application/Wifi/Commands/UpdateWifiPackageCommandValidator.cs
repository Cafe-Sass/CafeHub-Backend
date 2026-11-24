using CafeHub.Domain.Tenants;
using FluentValidation;

namespace CafeHub.Application.Wifi.Commands
{
    public sealed class UpdateWifiPackageCommandValidator : AbstractValidator<UpdateWifiPackageCommand>
    {
        public UpdateWifiPackageCommandValidator()
        {
            RuleFor(command => command.PackageId)
                .NotEmpty()
                .WithMessage("The WiFi package id is required.");

            RuleFor(command => command.Name)
                .Must(WifiPackage.IsValidName)
                .WithMessage(
                    $"The WiFi package name must be 1 to {WifiPackage.MaximumNameLength} characters without control characters.");

            RuleFor(command => command.DurationMinutes)
                .Must(WifiPackage.IsValidDuration)
                .WithMessage(
                    $"The duration must be between 1 and {WifiPackage.MaximumDurationMinutes} minutes.");

            RuleFor(command => command.SpeedMbps)
                .Must(WifiPackage.IsValidSpeed)
                .WithMessage(
                    $"The speed limit must be null or greater than zero, at most two decimal places, and at most {WifiPackage.MaximumSpeedMbps} Mbps.");

            RuleFor(command => command.DataMode)
                .Must(mode => Enum.IsDefined(mode))
                .WithMessage("The WiFi data mode must be Unlimited or Limited.");

            RuleFor(command => command.QuotaMb)
                .Must(WifiPackage.IsValidQuota)
                .WithMessage($"The quota must be null or between 1 and {WifiPackage.MaximumQuotaMb} MB.");

            RuleFor(command => command)
                .Custom((command, context) => WifiPackageValidation.ValidateQuotaMode(
                    command.DataMode, command.QuotaMb, context));

            RuleFor(command => command.Price)
                .Must(WifiPackage.IsValidPrice)
                .WithMessage("The price must be non-negative, at most two decimal places, and within numeric(12,2).");

            RuleFor(command => command.SortOrder)
                .Must(WifiPackage.IsValidSortOrder)
                .WithMessage("The sort order cannot be negative.");

            RuleFor(command => command.ConcurrencyToken)
                .NotNull()
                .WithMessage("The concurrency token is required.");
        }
    }
}
