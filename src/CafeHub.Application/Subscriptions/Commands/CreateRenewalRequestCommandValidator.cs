using FluentValidation;

namespace CafeHub.Application.Subscriptions.Commands
{
    public sealed class CreateRenewalRequestCommandValidator
        : AbstractValidator<CreateRenewalRequestCommand>
    {
        public const int MaximumActorReferenceLength = 256;

        public CreateRenewalRequestCommandValidator()
        {
            RuleFor(command => command.SubscriptionId).NotEmpty()
                .WithMessage("The subscription id is required.");
            RuleFor(command => command.PlanVersionId)
                .Must(planVersionId => planVersionId is null || planVersionId != Guid.Empty)
                .WithMessage("The plan version id cannot be empty.");
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
