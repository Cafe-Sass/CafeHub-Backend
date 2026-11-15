using CafeHub.Application.Abstractions.Messaging;

namespace CafeHub.Application.Roles
{
    public sealed record GetStaffEffectivePermissions(Guid StaffId) : IQuery<IReadOnlyList<string>>;
}
