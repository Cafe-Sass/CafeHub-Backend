using CafeHub.Domain.Tenants;
using FluentValidation;

namespace CafeHub.Application.Tables.Commands
{
    public sealed class UpdateTableCommandValidator : AbstractValidator<UpdateTableCommand>
    {
        public UpdateTableCommandValidator()
        {
            RuleFor(command => command.TableId)
                .NotEmpty();

            RuleFor(command => command.Label)
                .Cascade(CascadeMode.Stop)
                .Must(label => string.IsNullOrWhiteSpace(label) || label.Trim().Length <= BranchTable.MaximumLabelLength)
                .WithMessage($"The table label must be at most {BranchTable.MaximumLabelLength} characters after trimming.")
                .Must(label => string.IsNullOrWhiteSpace(label) || !label.Trim().Any(char.IsControl))
                .WithMessage("The table label must not contain control characters.");

            RuleFor(command => command.ConcurrencyToken)
                .NotEmpty()
                .Must(BeValidConcurrencyToken)
                .WithMessage("The concurrency token is not a valid unsigned integer.");
        }

        private static bool BeValidConcurrencyToken(string? token) =>
            uint.TryParse(token, out _);
    }
}
