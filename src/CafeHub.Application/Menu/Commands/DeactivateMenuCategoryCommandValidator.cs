using FluentValidation;

namespace CafeHub.Application.Menu.Commands
{
    public sealed class DeactivateMenuCategoryCommandValidator : AbstractValidator<DeactivateMenuCategoryCommand>
    {
        public DeactivateMenuCategoryCommandValidator()
        {
            RuleFor(command => command.Id).NotEmpty();
            RuleFor(command => command.ConcurrencyToken).Must(token => MenuCategoryVersion.TryParse(token, out _))
                .WithMessage("The concurrency token is invalid.");
        }
    }
}
