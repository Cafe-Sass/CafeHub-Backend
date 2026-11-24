using CafeHub.Domain.Tenants;
using FluentValidation;

namespace CafeHub.Application.Wifi.Commands
{
    public sealed class DeactivateWifiPackageCommandValidator : AbstractValidator<DeactivateWifiPackageCommand>
    {
        public DeactivateWifiPackageCommandValidator()
        {
            RuleFor(command => command.PackageId)
                .NotEmpty()
                .WithMessage("The WiFi package id is required.");
            RuleFor(command => command.ConcurrencyToken)
                .NotNull()
                .WithMessage("The concurrency token is required.");
        }
    }

    public sealed class ReactivateWifiPackageCommandValidator : AbstractValidator<ReactivateWifiPackageCommand>
    {
        public ReactivateWifiPackageCommandValidator()
        {
            RuleFor(command => command.PackageId)
                .NotEmpty()
                .WithMessage("The WiFi package id is required.");
            RuleFor(command => command.ConcurrencyToken)
                .NotNull()
                .WithMessage("The concurrency token is required.");
        }
    }
}
