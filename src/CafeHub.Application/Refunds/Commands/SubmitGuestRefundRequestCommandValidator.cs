using CafeHub.Application.Idempotency;
using CafeHub.Application.Refunds.Contracts;
using CafeHub.Domain.Tenants;
using FluentValidation;

namespace CafeHub.Application.Refunds.Commands
{
    public sealed class SubmitGuestRefundRequestCommandValidator : AbstractValidator<SubmitGuestRefundRequestCommand>
    {
        public SubmitGuestRefundRequestCommandValidator()
        {
            RuleFor(command => command.IdempotencyKey).MustHaveValidIdempotencyKey();
            RuleFor(command => command.BillReference)
                .Must(Bill.IsValidPublicReference)
                .WithMessage("The bill reference must be 22 URL-safe base64url characters.");
            RuleFor(command => command.PaymentId).NotEmpty();
            RuleFor(command => command.ClaimedAmount)
                .Must(Payment.IsValidAmount)
                .WithMessage("The claimed amount must be positive, have at most two decimal places, and fit numeric(12,2).");
            RuleFor(command => command.Reason)
                .Must(RefundRequest.IsValidReason)
                .WithMessage("The reason must be 3 to 256 characters after trimming.");
            RuleFor(command => command.Details)
                .Must(RefundRequest.IsValidDetails)
                .WithMessage("Details must not exceed 2000 characters.");
            RuleFor(command => command.Evidence)
                .Must(evidence => evidence is not null
                    && evidence.Count <= RefundRequest.MaximumAttachmentCount
                    && evidence.All(file => file.Content is { CanRead: true }))
                .WithMessage($"At most {RefundRequest.MaximumAttachmentCount} readable evidence files are allowed.");
        }
    }
}
