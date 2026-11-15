using CafeHub.Application.Abstractions.Messaging;

namespace CafeHub.Application.Roles
{
    public sealed record ReplaceStaffPermissions(Guid StaffId, IReadOnlyList<string>? PermissionCodes) : ICommand, ITenantTransactionalCommand;
}
