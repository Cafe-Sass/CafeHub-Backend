using FluentValidation;

namespace CafeHub.Application.Menu.Commands
{
    public sealed class SetProductStationAssignmentCommandValidator : AbstractValidator<SetProductStationAssignmentCommand>
    {
        public SetProductStationAssignmentCommandValidator()
        {
            RuleFor(command => command.ProductId).NotEmpty();
            RuleFor(command => command.BranchId).NotEmpty();
            RuleFor(command => command.StationId).Must(id => id != Guid.Empty);
            RuleFor(command => command.ConcurrencyToken).NotEmpty().MaximumLength(100);
        }
    }
}
