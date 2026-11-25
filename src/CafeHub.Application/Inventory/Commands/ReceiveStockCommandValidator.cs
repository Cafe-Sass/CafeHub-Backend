using CafeHub.Application.Idempotency;
using CafeHub.Domain.Tenants;
using FluentValidation;

namespace CafeHub.Application.Inventory.Commands
{
    public sealed class ReceiveStockCommandValidator : AbstractValidator<ReceiveStockCommand>
    {
        public ReceiveStockCommandValidator()
        {
            RuleFor(command => command.BranchId)
                .NotEmpty()
                .WithMessage("The branch id is required.");
            RuleFor(command => command.ItemId)
                .NotEmpty()
                .WithMessage("The item id is required.");
            RuleFor(command => command.ReceivedQuantity)
                .Must(InventoryBatch.IsValidQuantity)
                .WithMessage("The received quantity must be greater than zero, carry at most 3 decimals, and stay within numeric(12,3).");
            RuleFor(command => command.ReceivedDate)
                .Must(InventoryBatch.IsValidReceivedDate)
                .WithMessage("The received date is required.");
            RuleFor(command => command.ExpiryDate)
                .Must(InventoryBatch.IsValidExpiryDate)
                .WithMessage("The expiry date is invalid.");
            RuleFor(command => command.IdempotencyKey)
                .MustHaveValidIdempotencyKey();
        }
    }
}
