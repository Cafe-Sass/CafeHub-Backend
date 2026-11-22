using FluentValidation;

namespace CafeHub.Application.Orders.Commands
{
    public sealed class AcceptOrderCommandValidator : AbstractValidator<AcceptOrderCommand>
    {
        public AcceptOrderCommandValidator()
        {
            RuleFor(command => command.OrderId)
                .NotEmpty()
                .WithMessage("The order id is required.");
        }
    }
}
