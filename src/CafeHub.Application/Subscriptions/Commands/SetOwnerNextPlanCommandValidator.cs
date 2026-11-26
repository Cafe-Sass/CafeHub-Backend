using FluentValidation;

namespace CafeHub.Application.Subscriptions.Commands
{
    public sealed class SetOwnerNextPlanCommandValidator : AbstractValidator<SetOwnerNextPlanCommand>
    {
        public const int MaximumActorReferenceLength = 256;

        public SetOwnerNextPlanCommandValidator()
        {
            RuleFor(command => command.TenantKey).NotEmpty()
                .WithMessage("The tenant id is required.");
            RuleFor(command => command.PlanVersionId).NotEmpty()
                .WithMessage("The plan version id is required.");
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
