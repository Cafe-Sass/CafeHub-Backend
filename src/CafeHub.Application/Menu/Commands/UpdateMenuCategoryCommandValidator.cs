using CafeHub.Domain.Tenants;
using FluentValidation;

namespace CafeHub.Application.Menu.Commands
{
    public sealed class UpdateMenuCategoryCommandValidator : AbstractValidator<UpdateMenuCategoryCommand>
    {
        public UpdateMenuCategoryCommandValidator()
        {
            RuleFor(command => command.Id).NotEmpty();
            RuleFor(command => command.Name).Must(MenuCategory.IsValidName)
                .WithMessage("The name must contain 1 to 64 Unicode scalars after trimming, with no NUL.");
            RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
            RuleFor(command => command.ConcurrencyToken).Must(token => MenuCategoryVersion.TryParse(token, out _))
                .WithMessage("The concurrency token is invalid.");
        }
    }
}
