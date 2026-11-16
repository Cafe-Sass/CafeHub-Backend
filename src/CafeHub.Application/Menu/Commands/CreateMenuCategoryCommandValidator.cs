using CafeHub.Domain.Tenants;
using FluentValidation;

namespace CafeHub.Application.Menu.Commands
{
    public sealed class CreateMenuCategoryCommandValidator : AbstractValidator<CreateMenuCategoryCommand>
    {
        public CreateMenuCategoryCommandValidator()
        {
            RuleFor(command => command.ParentId).Must(id => id != Guid.Empty)
                .WithMessage(MenuCategoryErrors.InvalidParentMessage)
                .OverridePropertyName("parentId");
            RuleFor(command => command.Name).Must(MenuCategory.IsValidName)
                .WithMessage("The name must contain 1 to 64 Unicode scalars after trimming, with no NUL.");
            RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
        }
    }
}
