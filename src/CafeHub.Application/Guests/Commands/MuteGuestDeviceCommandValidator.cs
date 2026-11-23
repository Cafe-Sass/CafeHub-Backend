using CafeHub.Domain.Tenants;
using FluentValidation;

namespace CafeHub.Application.Guests.Commands
{
    public sealed class MuteGuestDeviceCommandValidator : AbstractValidator<MuteGuestDeviceCommand>
    {
        public MuteGuestDeviceCommandValidator()
        {
            RuleFor(command => command.OrderId).NotEmpty()
                .WithMessage("The order id is required.");
            RuleFor(command => command.Reason)
                .Must(GuestMute.IsValidReason)
                .WithMessage("The mute reason must be 3 to 256 characters with no control characters.");
            RuleFor(command => command.ExpiryHours)
                .InclusiveBetween(1, GuestMute.MaximumExpiryHours)
                .When(command => command.ExpiryHours.HasValue)
                .WithMessage("The mute expiry must be between 1 and 168 hours.");
        }
    }
}
