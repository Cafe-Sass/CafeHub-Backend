using FluentValidation;

namespace CafeHub.Application.Refunds.Commands
{
    public sealed class AssignRefundRequestCommandValidator : AbstractValidator<AssignRefundRequestCommand>
    {
        public AssignRefundRequestCommandValidator()
        {
            RuleFor(command => command.RefundRequestId).NotEmpty();
            RuleFor(command => command.AssignedUserId).NotEmpty();
        }
    }
}
