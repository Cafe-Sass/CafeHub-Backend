using CafeHub.Domain.Tenants;
using FluentValidation;

namespace CafeHub.Application.Inventory.Commands
{
    public sealed class CreateInventoryItemCommandValidator : AbstractValidator<CreateInventoryItemCommand>
    {
        public CreateInventoryItemCommandValidator()
        {
            RuleFor(command => command.Name).Must(InventoryItem.IsValidName)
                .WithMessage("The name must contain 1 to 96 characters after trimming, with no control characters.");
            RuleFor(command => command.Unit).IsInEnum()
                .WithMessage("The unit must be Kg, G, L, Ml, Piece, or Pack.");
            RuleFor(command => command.LowStockThreshold).Must(InventoryItem.IsValidLowStockThreshold)
                .WithMessage("The low-stock threshold must be between 0 and 999999999.999 with at most 3 decimals.");
            RuleFor(command => command.NearExpiryDays).Must(InventoryItem.IsValidNearExpiryDays)
                .WithMessage("The near-expiry days must not be negative.");
        }
    }
}
