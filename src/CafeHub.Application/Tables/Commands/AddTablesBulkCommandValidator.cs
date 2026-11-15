using FluentValidation;

namespace CafeHub.Application.Tables.Commands
{
    public sealed class AddTablesBulkCommandValidator : AbstractValidator<AddTablesBulkCommand>
    {
        public const int MinimumCount = 1;
        public const int MaximumCount = 200;

        public AddTablesBulkCommandValidator()
        {
            RuleFor(command => command.BranchId)
                .NotEmpty();

            RuleFor(command => command.Count)
                .InclusiveBetween(MinimumCount, MaximumCount)
                .WithMessage($"The bulk count must be between {MinimumCount} and {MaximumCount}.");
        }
    }
}
