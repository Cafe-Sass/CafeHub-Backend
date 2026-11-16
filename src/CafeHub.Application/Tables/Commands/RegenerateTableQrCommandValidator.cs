using System.Globalization;
using FluentValidation;

namespace CafeHub.Application.Tables.Commands
{
    public sealed class RegenerateTableQrCommandValidator : AbstractValidator<RegenerateTableQrCommand>
    {
        public RegenerateTableQrCommandValidator()
        {
            RuleFor(command => command.BranchId).NotEmpty();
            RuleFor(command => command.TableId).NotEmpty();
            RuleFor(command => command.ConcurrencyToken)
                .Must(token => uint.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out _))
                .WithMessage("The concurrency token is not a valid unsigned integer.");
        }
    }
}
