using CafeHub.Application.Abstractions.Messaging;

namespace CafeHub.Application.Roles
{
    public sealed record ListTenantRoles : IQuery<IReadOnlyList<TenantRoleResponse>>;
}
