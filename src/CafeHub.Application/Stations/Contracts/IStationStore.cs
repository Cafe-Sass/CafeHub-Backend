using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Stations.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Stations.Contracts
{
    /// <summary>
    /// Persistence port for a tenant's preparation stations. The tenant is resolved from the write-once tenant
    /// context; the store never accepts a tenant identifier from callers. <c>scopeBranchId</c> carries the branch
    /// scope of a delegated staff user and is null for the Owner. Each write commits the station change and one
    /// tenant audit entry together in a single tenant transaction.
    /// </summary>
    public interface IStationStore
    {
        Task<Result<StationResponse>> CreateAsync(
            Guid branchId,
            string name,
            int sortOrder,
            Guid actorUserId,
            CancellationToken cancellationToken);

        Task<Result<StationResponse>> UpdateAsync(
            Guid stationId,
            Guid? scopeBranchId,
            string name,
            int sortOrder,
            string concurrencyToken,
            Guid actorUserId,
            CancellationToken cancellationToken);

        Task<Result<StationResponse>> DeactivateAsync(
            Guid stationId,
            Guid? scopeBranchId,
            string concurrencyToken,
            Guid actorUserId,
            CancellationToken cancellationToken);

        Task<Result<StationResponse>> ReactivateAsync(
            Guid stationId,
            Guid? scopeBranchId,
            string concurrencyToken,
            Guid actorUserId,
            CancellationToken cancellationToken);

        Task<StationResponse?> GetByIdAsync(
            Guid stationId,
            Guid? scopeBranchId,
            CancellationToken cancellationToken);

        Task<StationListPage> ListAsync(StationListRequest request, CancellationToken cancellationToken);

        Task<Result> SetStaffStationsAsync(
            Guid staffId,
            IReadOnlyList<Guid> stationIds,
            Guid? scopeBranchId,
            Guid actorUserId,
            CancellationToken cancellationToken);
    }
}
