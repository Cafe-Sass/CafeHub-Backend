using FluentValidation;

namespace CafeHub.Application.Onboarding.Commands
{
    public sealed class RejectOnboardingRequestCommandValidator : AbstractValidator<RejectOnboardingRequestCommand>
    {
        private const int MaxReasonLength = 500;

        public RejectOnboardingRequestCommandValidator()
        {
            RuleFor(command => command.RequestId)
                .NotEmpty();

            RuleFor(command => command.Reason)
                .MaximumLength(MaxReasonLength)
                .When(command => command.Reason is not null);
        }
    }
}
