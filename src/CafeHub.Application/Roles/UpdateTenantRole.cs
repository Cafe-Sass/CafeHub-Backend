using CafeHub.Application.Abstractions.Messaging;

namespace CafeHub.Application.Roles
{
    public sealed record UpdateTenantRole(Guid RoleId, string? Name, IReadOnlyList<string>? PermissionCodes, string? ConcurrencyToken) : ICommand<TenantRoleResponse>, ITenantTransactionalCommand;
}
