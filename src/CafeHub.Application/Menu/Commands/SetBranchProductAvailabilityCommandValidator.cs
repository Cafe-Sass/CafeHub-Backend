using CafeHub.Domain.Tenants;
using FluentValidation;

namespace CafeHub.Application.Menu.Commands
{
    public sealed class SetBranchProductAvailabilityCommandValidator
        : AbstractValidator<SetBranchProductAvailabilityCommand>
    {
        public SetBranchProductAvailabilityCommandValidator()
        {
            RuleFor(command => command.BranchId).NotEmpty();
            RuleFor(command => command.ProductId).NotEmpty();
            RuleFor(command => command.Reason)
                .Must(reason => reason == BranchProductAvailabilityReasons.Manual)
                .WithMessage("Only the manual reason can be set.")
                .OverridePropertyName("reason");
        }
    }
}
