using FluentValidation;

namespace CafeHub.Application.Orders.Commands
{
    public sealed class ExpireStaleOrdersCommandValidator : AbstractValidator<ExpireStaleOrdersCommand>
    {
        public ExpireStaleOrdersCommandValidator()
        {
            RuleFor(command => command)
                .Must(command =>
                {
                    var hasBranch = command.BranchId is { } branch && branch != Guid.Empty;
                    var hasOrder = command.OrderId is { } order && order != Guid.Empty;
                    return hasBranch != hasOrder;
                })
                .WithMessage("Expire exactly one branch or one order.");
        }
    }
}
