using CafeHub.Domain.Tenants;
using FluentValidation;

namespace CafeHub.Application.Menu.Commands
{
    public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
    {
        public CreateProductCommandValidator()
        {
            RuleForEach(command => command.Sizes).SetValidator(new ProductSizeInputValidator());
            RuleForEach(command => command.Addons).SetValidator(new ProductAddonInputValidator());
            RuleForEach(command => command.Tags).Must(ProductTag.IsValid)
                .WithMessage("Tags must be 1 to 32 lower-case letters, digits, hyphens, or spaces.");

            RuleFor(command => command.CategoryId).NotEmpty();
            RuleFor(command => command.StationId).Must(id => id != Guid.Empty)
                .WithMessage("The station id cannot be empty.");
            RuleFor(command => command.Name).Must(Product.IsValidName)
                .WithMessage("The name must contain 1 to 96 characters after trimming, with no control characters.");
            RuleFor(command => command.Description).Must(value => Product.IsValidOptionalText(value, Product.MaximumDescriptionLength))
                .WithMessage("The description must not exceed 1000 characters or contain NUL.");
            RuleFor(command => command.Ingredients).Must(value => Product.IsValidOptionalText(value, Product.MaximumIngredientsLength))
                .WithMessage("The ingredients must not exceed 1000 characters or contain NUL.");
            RuleFor(command => command.Kind).IsInEnum();
            RuleFor(command => command.PreparationMinutes).Must(Product.IsValidPreparationMinutes)
                .WithMessage("The preparation minutes must be between 0 and 240.");
            RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);

            RuleFor(command => command.Sizes).NotNull()
                .Must(sizes => sizes is { Count: > 0 })
                .WithMessage("A product requires at least one size.");
            RuleFor(command => command.Sizes).Must(sizes => sizes is not null
                    && sizes.All(size => size.Id is null))
                .WithMessage("Create does not accept size ids.");
            RuleFor(command => command.Sizes).Must(sizes => sizes is not null
                    && sizes.Count(size => size.IsDefault) == 1)
                .WithMessage("A product requires exactly one default size.");
            RuleFor(command => command.Sizes).Must(sizes => sizes is not null
                    && sizes.Select(size => size.Name?.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == sizes.Count)
                .WithMessage("Active size names must be unique within a product.");

            RuleFor(command => command.Addons).NotNull();
            RuleFor(command => command.Addons).Must(addons => addons is not null
                    && addons.All(addon => addon.Id is null))
                .WithMessage("Create does not accept add-on ids.");
            RuleFor(command => command.Addons).Must(addons => addons is not null
                    && addons.Select(addon => addon.Name?.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == addons.Count)
                .WithMessage("Active add-on names must be unique within a product.");

            RuleFor(command => command.Tags).NotNull();
            RuleFor(command => command.Tags).Must(tags => tags is not null && tags.Count <= Product.MaximumTagCount)
                .WithMessage("A product can have at most 10 tags.");
            RuleFor(command => command.Tags).Must(tags => tags is not null
                    && tags.Select(tag => (tag ?? string.Empty).Trim().ToLowerInvariant())
                        .Distinct(StringComparer.Ordinal).Count() == tags.Count)
                .WithMessage("Tags must be unique within a product.");
        }
    }
}
