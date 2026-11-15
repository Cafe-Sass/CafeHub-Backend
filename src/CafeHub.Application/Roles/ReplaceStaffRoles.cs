using CafeHub.Application.Abstractions.Messaging;

namespace CafeHub.Application.Roles
{
    public sealed record ReplaceStaffRoles(Guid StaffId, IReadOnlyList<Guid>? RoleIds) : ICommand, ITenantTransactionalCommand;
}
