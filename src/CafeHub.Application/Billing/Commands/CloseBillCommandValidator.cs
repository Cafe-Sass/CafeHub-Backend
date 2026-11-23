using CafeHub.Domain.Tenants;
using FluentValidation;

namespace CafeHub.Application.Billing.Commands
{
    public sealed class CloseBillCommandValidator : AbstractValidator<CloseBillCommand>
    {
        public CloseBillCommandValidator()
        {
            RuleFor(command => command.BillId)
                .NotEmpty()
                .WithMessage("The bill id is required.");

            RuleFor(command => command.Reason)
                .Must(reason => Bill.IsValidCloseReason(reason))
                .WithMessage(
                    $"The close reason must be {Bill.MinimumCloseReasonLength} to {Bill.MaximumCloseReasonLength} characters with no control characters.");
        }
    }
}
