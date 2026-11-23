using FluentValidation;

namespace CafeHub.Application.Orders.Commands
{
    public sealed class SubmitAssistedOrderCommandValidator : AbstractValidator<SubmitAssistedOrderCommand>
    {
        public SubmitAssistedOrderCommandValidator()
        {
            RuleFor(command => command.TableId).NotEmpty()
                .WithMessage("The table id is required.");
        }
    }
}
