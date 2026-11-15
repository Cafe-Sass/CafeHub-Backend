using CafeHub.Application.Abstractions.Messaging;

namespace CafeHub.Application.Roles
{
    public sealed record CreateTenantRole(string? Name, IReadOnlyList<string>? PermissionCodes) : ICommand<TenantRoleResponse>, ITenantTransactionalCommand;
}
