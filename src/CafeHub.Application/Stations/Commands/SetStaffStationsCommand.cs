using CafeHub.Application.Abstractions.Messaging;

namespace CafeHub.Application.Stations.Commands
{
    /// <summary>
    /// Replaces the station assignments of a staff member with the given set. Every station must be
    /// Active and belong to the staff member's branch; the target must be a Staff user. Marked as a
    /// tenant transactional command so the replacement and the tenant audit entry commit together.
    /// </summary>
    public sealed record SetStaffStationsCommand(Guid StaffId, IReadOnlyList<Guid> StationIds)
        : ICommand, ITenantTransactionalCommand;
}
