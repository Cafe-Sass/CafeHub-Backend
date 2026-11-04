using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Upgrades.DTOs;

namespace CafeHub.Application.Upgrades.Commands
{
    public sealed record QueueTenantSchemaUpgradeCommand(Guid TenantKey)
        : ICommand<TenantSchemaUpgradeResponse>, IMasterTransactionalCommand;
}
