using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Menu.DTOs;

namespace CafeHub.Application.Menu.Contracts
{
    public interface IProductStationAssignmentStore
    {
        Task<Result<ProductStationAssignmentResponse>> GetAsync(
            Guid productId, Guid branchId, CancellationToken cancellationToken);

        Task<Result<ProductStationAssignmentResponse>> SetAsync(
            Guid productId, Guid branchId, Guid? stationId, string token, Guid actorId, CancellationToken cancellationToken);

        Task<Result<ProductRouting>> ResolveRoutingAsync(Guid productId, Guid branchId, CancellationToken cancellationToken);
    }
}
