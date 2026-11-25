using FluentValidation;

namespace CafeHub.Application.Inventory.Commands
{
    public sealed class DeactivateInventoryItemCommandValidator : AbstractValidator<DeactivateInventoryItemCommand>
    {
        public DeactivateInventoryItemCommandValidator()
        {
            RuleFor(command => command.Id).NotEmpty();
            RuleFor(command => command.ConcurrencyToken).Must(token => InventoryItemVersion.TryParse(token, out _))
                .WithMessage("The concurrency token is invalid.");
        }
    }
}
