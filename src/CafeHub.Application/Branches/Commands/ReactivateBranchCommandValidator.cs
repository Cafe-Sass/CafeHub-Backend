using System.Text.RegularExpressions;
using FluentValidation;

namespace CafeHub.Application.Branches.Commands
{
    public sealed class ReactivateBranchCommandValidator : AbstractValidator<ReactivateBranchCommand>
    {
        public ReactivateBranchCommandValidator()
        {
            RuleFor(command => command.BranchId)
                .NotEmpty();

            RuleFor(command => command.ConcurrencyToken)
                .NotEmpty()
                .Must(BeValidConcurrencyToken)
                .WithMessage("The concurrency token is not a valid unsigned integer.");
        }

        private static bool BeValidConcurrencyToken(string? token)
        {
            return uint.TryParse(token, out _);
        }
    }
}
