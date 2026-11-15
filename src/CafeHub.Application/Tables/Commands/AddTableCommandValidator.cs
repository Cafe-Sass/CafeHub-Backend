using CafeHub.Domain.Tenants;
using FluentValidation;

namespace CafeHub.Application.Tables.Commands
{
    public sealed class AddTableCommandValidator : AbstractValidator<AddTableCommand>
    {
        public AddTableCommandValidator()
        {
            RuleFor(command => command.BranchId)
                .NotEmpty();

            RuleFor(command => command.Label)
                .Cascade(CascadeMode.Stop)
                .Must(label => string.IsNullOrWhiteSpace(label) || label.Trim().Length <= BranchTable.MaximumLabelLength)
                .WithMessage($"The table label must be at most {BranchTable.MaximumLabelLength} characters after trimming.")
                .Must(label => string.IsNullOrWhiteSpace(label) || !label.Trim().Any(char.IsControl))
                .WithMessage("The table label must not contain control characters.");
        }
    }
}
