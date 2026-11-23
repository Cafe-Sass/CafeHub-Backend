using CafeHub.Application.Idempotency;
using CafeHub.Domain.Tenants;
using FluentValidation;

namespace CafeHub.Application.Billing.Commands
{
    public sealed class ConfirmCashPaymentCommandValidator : AbstractValidator<ConfirmCashPaymentCommand>
    {
        public ConfirmCashPaymentCommandValidator()
        {
            RuleFor(command => command.BillId)
                .NotEmpty()
                .WithMessage("The bill id is required.");

            RuleFor(command => command.OrderId)
                .Must(orderId => orderId is null || orderId != Guid.Empty)
                .WithMessage("The linked order id cannot be empty.");

            RuleFor(command => command.Amount)
                .Must(Payment.IsValidAmount)
                .WithMessage("The amount must be greater than zero, carry at most two decimal places, and stay within numeric(12,2).");

            RuleFor(command => command.IdempotencyKey)
                .MustHaveValidIdempotencyKey();
        }
    }
}
