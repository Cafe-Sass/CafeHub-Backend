using CafeHub.Domain.Tenants;
using FluentValidation;

namespace CafeHub.Application.Menu.Commands
{
    public sealed class ProductAddonInputValidator : AbstractValidator<ProductAddonInput>
    {
        public ProductAddonInputValidator()
        {
            RuleFor(addon => addon.Id).Must(id => id != Guid.Empty)
                .WithMessage("The add-on id cannot be empty.");
            RuleFor(addon => addon.Name).Must(ProductAddon.IsValidName)
                .WithMessage("The add-on name must contain 1 to 48 characters after trimming, with no control characters.");
            RuleFor(addon => addon.Price).Must(ProductAddon.IsValidPrice)
                .WithMessage("The add-on price must be between 0.00 and 99999999.99 with at most two decimal places.");
        }
    }
}
