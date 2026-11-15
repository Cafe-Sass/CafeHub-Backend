using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Stations.DTOs;

namespace CafeHub.Application.Stations.Commands
{
    /// <summary>
    /// Creates a station in an active branch. Marked as a tenant transactional command so the creation and the
    /// tenant audit entry commit together in one tenant transaction.
    /// </summary>
    public sealed record CreateStationCommand(Guid BranchId, string Name, int SortOrder)
        : ICommand<StationResponse>, ITenantTransactionalCommand;
}
