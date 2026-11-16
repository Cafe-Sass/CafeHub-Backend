using CafeHub.Domain.Tenants;
using FluentValidation;

namespace CafeHub.Application.Menu.Commands
{
    public sealed class ProductSizeInputValidator : AbstractValidator<ProductSizeInput>
    {
        public ProductSizeInputValidator()
        {
            RuleFor(size => size.Id).Must(id => id != Guid.Empty)
                .WithMessage("The size id cannot be empty.");
            RuleFor(size => size.Name).Must(ProductSize.IsValidName)
                .WithMessage("The size name must contain 1 to 32 characters after trimming, with no control characters.");
            RuleFor(size => size.Price).Must(ProductSize.IsValidPrice)
                .WithMessage("The size price must be between 0.00 and 99999999.99 with at most two decimal places.");
        }
    }
}
