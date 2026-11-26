using FluentValidation;

namespace CafeHub.Application.Subscriptions.Commands
{
    public sealed class ExpireRenewalRequestCommandValidator
        : AbstractValidator<ExpireRenewalRequestCommand>
    {
        public ExpireRenewalRequestCommandValidator()
        {
            RuleFor(command => command.RenewalRequestId).NotEmpty()
                .WithMessage("The renewal request id is required.");
        }
    }
}
