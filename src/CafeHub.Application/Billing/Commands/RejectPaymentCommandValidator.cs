using CafeHub.Domain.Tenants;
using FluentValidation;

namespace CafeHub.Application.Billing.Commands
{
    public sealed class RejectPaymentCommandValidator : AbstractValidator<RejectPaymentCommand>
    {
        public RejectPaymentCommandValidator()
        {
            RuleFor(command => command.PaymentId)
                .NotEmpty()
                .WithMessage("The payment id is required.");

            RuleFor(command => command.Reason)
                .Must(Payment.IsValidRejectionReason)
                .WithMessage("The rejection reason must be 3 to 256 characters without control characters.");
        }
    }
}
