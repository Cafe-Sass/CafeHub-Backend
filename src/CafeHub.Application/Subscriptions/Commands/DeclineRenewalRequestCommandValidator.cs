using FluentValidation;

namespace CafeHub.Application.Subscriptions.Commands
{
    public sealed class DeclineRenewalRequestCommandValidator
        : AbstractValidator<DeclineRenewalRequestCommand>
    {
        public const int MaximumActorReferenceLength = 256;

        public DeclineRenewalRequestCommandValidator()
        {
            RuleFor(command => command.RenewalRequestId).NotEmpty()
                .WithMessage("The renewal request id is required.");
            RuleFor(command => command.ActorReference)
                .Must(reference => reference is null
                    || (reference.Trim().Length is >= 1 and <= MaximumActorReferenceLength
                        && !reference.Any(char.IsControl)))
                .WithMessage(
                    $"The actor reference must be 1 to {MaximumActorReferenceLength} characters without control characters.")
                .WithErrorCode("Subscription.InvalidActorReference");
        }
    }
}
