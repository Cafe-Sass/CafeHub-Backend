using FluentValidation;

namespace CafeHub.Application.Upgrades.Commands
{
    public sealed class QueueTenantSchemaUpgradeCommandValidator : AbstractValidator<QueueTenantSchemaUpgradeCommand>
    {
        public QueueTenantSchemaUpgradeCommandValidator()
        {
            RuleFor(command => command.TenantKey)
                .NotEmpty();
        }
    }
}
