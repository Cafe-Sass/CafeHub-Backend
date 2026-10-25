using CafeHub.Application.Idempotency;
using FluentValidation;

namespace CafeHub.Application.Onboarding.Commands
{
    public sealed class PayOnboardingRequestCommandValidator : AbstractValidator<PayOnboardingRequestCommand>
    {
        public PayOnboardingRequestCommandValidator()
        {
            RuleFor(command => command.RequestId)
                .NotEmpty();

            RuleFor(command => command.IdempotencyKey)
                .MustHaveValidIdempotencyKey();
        }
    }
}
