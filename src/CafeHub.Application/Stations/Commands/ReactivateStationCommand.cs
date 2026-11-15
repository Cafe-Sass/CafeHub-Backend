using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Stations.DTOs;

namespace CafeHub.Application.Stations.Commands
{
    /// <summary>
    /// Reactivates a station; it fails with a conflict when another active station in the branch already holds the
    /// same name. Marked as a tenant transactional command so the status change and the tenant audit entry commit
    /// together.
    /// </summary>
    public sealed record ReactivateStationCommand(Guid StationId, string ConcurrencyToken)
        : ICommand<StationResponse>, ITenantTransactionalCommand;
}
