using CafeHub.Domain.Tenants;
using FluentValidation;

namespace CafeHub.Application.Orders.Commands
{
    public sealed class RejectOrderCommandValidator : AbstractValidator<RejectOrderCommand>
    {
        public RejectOrderCommandValidator()
        {
            RuleFor(command => command.OrderId)
                .NotEmpty()
                .WithMessage("The order id is required.");
            RuleFor(command => command.Reason)
                .Must(Order.IsValidRejectionReason)
                .WithMessage("The rejection reason must be 3 to 256 characters after trimming, with no control characters.");
        }
    }
}
