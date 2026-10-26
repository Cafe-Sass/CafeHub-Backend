using FluentValidation;

namespace CafeHub.Application.Onboarding.Commands
{
    public sealed class ApproveOnboardingRequestCommandValidator : AbstractValidator<ApproveOnboardingRequestCommand>
    {
        public ApproveOnboardingRequestCommandValidator()
        {
            RuleFor(command => command.RequestId)
                .NotEmpty();
        }
    }
}
