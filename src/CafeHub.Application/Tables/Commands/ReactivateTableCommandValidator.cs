using FluentValidation;

namespace CafeHub.Application.Tables.Commands
{
    public sealed class ReactivateTableCommandValidator : AbstractValidator<ReactivateTableCommand>
    {
        public ReactivateTableCommandValidator()
        {
            RuleFor(command => command.TableId)
                .NotEmpty();

            RuleFor(command => command.ConcurrencyToken)
                .NotEmpty()
                .Must(BeValidConcurrencyToken)
                .WithMessage("The concurrency token is not a valid unsigned integer.");
        }

        private static bool BeValidConcurrencyToken(string? token) =>
            uint.TryParse(token, out _);
    }
}
