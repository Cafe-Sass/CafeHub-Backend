using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Stations.DTOs;

namespace CafeHub.Application.Stations.Commands
{
    /// <summary>
    /// Renames a station and changes its sort order only. The status is never changed by this command; a stale
    /// concurrency token is refused. Marked as a tenant transactional command so the update and the tenant audit
    /// entry commit together.
    /// </summary>
    public sealed record UpdateStationCommand(
        Guid StationId,
        string Name,
        int SortOrder,
        string ConcurrencyToken) : ICommand<StationResponse>, ITenantTransactionalCommand;
}
