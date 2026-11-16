using FluentValidation;

namespace CafeHub.Application.Menu.Commands
{
    public sealed class ReactivateProductCommandValidator : AbstractValidator<ReactivateProductCommand>
    {
        public ReactivateProductCommandValidator()
        {
            RuleFor(command => command.Id).NotEmpty();
            RuleFor(command => command.ConcurrencyToken).Must(token => ProductVersion.TryParse(token, out _))
                .WithMessage("The concurrency token is invalid.");
        }
    }
}
