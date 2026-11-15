using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Stations.DTOs;

namespace CafeHub.Application.Stations.Commands
{
    /// <summary>
    /// Deactivates a station without deleting it; staff assignments are kept and ignored while it is inactive.
    /// Marked as a tenant transactional command so the status change and the tenant audit entry commit together.
    /// </summary>
    public sealed record DeactivateStationCommand(Guid StationId, string ConcurrencyToken)
        : ICommand<StationResponse>, ITenantTransactionalCommand;
}
