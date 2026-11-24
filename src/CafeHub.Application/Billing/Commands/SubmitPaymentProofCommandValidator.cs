using CafeHub.Application.Idempotency;
using CafeHub.Domain.Tenants;
using FluentValidation;

namespace CafeHub.Application.Billing.Commands
{
    public sealed class SubmitPaymentProofCommandValidator : AbstractValidator<SubmitPaymentProofCommand>
    {
        public SubmitPaymentProofCommandValidator()
        {
            RuleFor(command => command.IdempotencyKey)
                .MustHaveValidIdempotencyKey();

            RuleFor(command => command.BillReference)
                .Must(Bill.IsValidPublicReference)
                .WithMessage("The bill reference must be 22 URL-safe base64url characters.");

            RuleFor(command => command.ClaimedAmount)
                .Must(Payment.IsValidAmount)
                .WithMessage("The claimed amount must be greater than zero, carry at most two decimal places, and stay within numeric(12,2).");

            RuleFor(command => command.Note)
                .Must(Payment.IsValidNote)
                .WithMessage($"The note must be 1 to {Payment.MaximumNoteLength} characters without control characters.");

            RuleFor(command => command.Content)
                .NotNull()
                .WithMessage("The payment screenshot image is required.");
        }
    }
}
