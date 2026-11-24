using CafeHub.Domain.Tenants;
using FluentValidation;

namespace CafeHub.Application.Billing.Commands
{
    public sealed class ConfirmPaymentCommandValidator : AbstractValidator<ConfirmPaymentCommand>
    {
        public ConfirmPaymentCommandValidator()
        {
            RuleFor(command => command.PaymentId)
                .NotEmpty()
                .WithMessage("The payment id is required.");

            RuleFor(command => command.Amount)
                .Must(amount => amount is null || Payment.IsValidAmount(amount.Value))
                .WithMessage("The confirmed amount must be greater than zero, carry at most two decimal places, and stay within numeric(12,2).");
        }
    }
}
