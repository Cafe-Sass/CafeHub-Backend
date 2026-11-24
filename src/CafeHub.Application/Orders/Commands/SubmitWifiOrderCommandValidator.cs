using CafeHub.Application.Idempotency;
using CafeHub.Domain.Tenants;
using FluentValidation;

namespace CafeHub.Application.Orders.Commands
{
    public sealed class SubmitWifiOrderCommandValidator : AbstractValidator<SubmitWifiOrderCommand>
    {
        public SubmitWifiOrderCommandValidator()
        {
            RuleFor(command => command.IdempotencyKey).MustHaveValidIdempotencyKey();

            RuleFor(command => command.Code).Must(BranchWifiQr.IsValidCode)
                .WithMessage("The WiFi QR code must be 22 URL-safe base64url characters.");
            RuleFor(command => command.PackageId).NotEmpty()
                .WithMessage("The WiFi package id is required.");
            RuleFor(command => command.GuestDisplayName).Must(Order.IsValidGuestDisplayName)
                .WithMessage("The guest name must be 1 to 64 characters after trimming, with no control characters.");
            RuleFor(command => command.GuestDisplayName)
                .Must(name => name is null || !name.Any(char.IsDigit))
                .WithMessage("The guest name must not contain digits or a phone number.");
        }
    }
}
