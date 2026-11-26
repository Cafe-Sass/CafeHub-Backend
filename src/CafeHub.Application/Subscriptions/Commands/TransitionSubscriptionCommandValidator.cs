using CafeHub.Domain.Subscriptions;
using FluentValidation;

namespace CafeHub.Application.Subscriptions.Commands
{
    public sealed class TransitionSubscriptionCommandValidator
        : AbstractValidator<TransitionSubscriptionCommand>
    {
        public const int MaximumReasonLength = 256;

        public TransitionSubscriptionCommandValidator()
        {
            RuleFor(command => command.SubscriptionId).NotEmpty()
                .WithMessage("The subscription id is required.");
            RuleFor(command => command.TargetStatus)
                .Must(Enum.IsDefined)
                .WithMessage("The target subscription status is not defined.");
            RuleFor(command => command.Reason)
                .Must(reason => reason is null
                    || (reason.Trim().Length is >= 1 and <= MaximumReasonLength
                        && !reason.Any(char.IsControl)))
                .WithMessage(
                    $"The reason must be 1 to {MaximumReasonLength} characters without control characters.")
                .WithErrorCode("Subscription.InvalidReason");
        }
    }
}
