using CafeHub.Application.Abstractions.Messaging;

namespace CafeHub.Application.Roles
{
    public sealed record RetireTenantRole(Guid RoleId) : ICommand, ITenantTransactionalCommand;
}
