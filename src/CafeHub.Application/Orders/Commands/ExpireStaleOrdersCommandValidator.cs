using FluentValidation;

namespace CafeHub.Application.Orders.Commands
{
    public sealed class ExpireStaleOrdersCommandValidator : AbstractValidator<ExpireStaleOrdersCommand>
    {
        public ExpireStaleOrdersCommandValidator()
        {
            RuleFor(command => command.BranchId)
                .NotEmpty()
                .When(command => command.OrderId is null)
                .WithMessage("The branch id or the order id is required.");
            RuleFor(command => command.OrderId)
                .NotEmpty()
                .When(command => command.BranchId is null)
                .WithMessage("The branch id or the order id is required.");
            RuleFor(command => command)
                .Must(command => command.BranchId is null || command.OrderId is null)
                .WithMessage("Expire either one branch or one order, not both.");
        }
    }
}
