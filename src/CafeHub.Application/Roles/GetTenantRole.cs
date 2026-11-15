using CafeHub.Application.Abstractions.Messaging;

namespace CafeHub.Application.Roles
{
    public sealed record GetTenantRole(Guid RoleId) : IQuery<TenantRoleResponse>;
}
