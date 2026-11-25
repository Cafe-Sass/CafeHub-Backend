using CafeHub.Application.Idempotency;
using CafeHub.Application.Inventory;
using CafeHub.Domain.Tenants;
using FluentValidation;

namespace CafeHub.Application.Inventory.Commands
{
    public sealed class RecordStockMovementCommandValidator : AbstractValidator<RecordStockMovementCommand>
    {
        public RecordStockMovementCommandValidator()
        {
            RuleFor(command => command.BranchId)
                .NotEmpty()
                .WithMessage("The branch id is required.");
            RuleFor(command => command.ItemId)
                .NotEmpty()
                .WithMessage("The item id is required.");
            RuleFor(command => command.Type)
                .Must(StockMovement.IsManualType)
                .WithMessage(StockMovementErrors.InvalidType().Message);
            RuleFor(command => command.Quantity)
                .Must((command, _) => IsValidQuantity(command))
                .WithMessage(
                    "The quantity must be positive for consume and waste, non-zero for an adjustment, carry at most 3 decimals, and stay within numeric(12,3).");
            RuleFor(command => command.Reason)
                .NotEmpty()
                .WithMessage(StockMovementErrors.ReasonRequired().Message)
                .Must(StockMovement.IsValidReason)
                .WithMessage(
                    $"The reason must be 1 to {StockMovement.MaximumReasonLength} characters without control characters.");
            RuleFor(command => command.BatchId)
                .NotEmpty()
                .When(command => command.Type == StockMovementType.Adjust)
                .WithMessage(StockMovementErrors.BatchRequired().Message);
            RuleFor(command => command.BatchId)
                .Empty()
                .When(command => command.Type is StockMovementType.Consume or StockMovementType.Waste)
                .WithMessage(StockMovementErrors.BatchNotAllowed().Message);
            RuleFor(command => command.IdempotencyKey)
                .MustHaveValidIdempotencyKey();
        }

        private static bool IsValidQuantity(RecordStockMovementCommand command) =>
            command.Type switch
            {
                StockMovementType.Consume or StockMovementType.Waste =>
                    command.Quantity > 0m
                    && command.Quantity <= StockMovement.MaximumQuantity
                    && command.Quantity == decimal.Round(command.Quantity, StockMovement.MaximumQuantityScale),
                StockMovementType.Adjust => InventoryBatch.IsValidMovementQuantity(command.Quantity),
                _ => false,
            };
    }
}
