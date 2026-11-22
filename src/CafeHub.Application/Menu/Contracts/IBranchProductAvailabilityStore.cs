using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Menu.DTOs;
using CafeHub.Application.Pagination;

namespace CafeHub.Application.Menu.Contracts
{
    public interface IBranchProductAvailabilityStore
    {
        Task<Result<BranchProductAvailabilityResponse>> SetAsync(
            Guid branchId, Guid productId, bool available, string reason, Guid actorId,
            CancellationToken cancellationToken);

        Task<Result<OffsetPage<BranchProductAvailabilityItem>>> ListAsync(
            Guid branchId, bool? available, Guid? categoryId, int page, int pageSize, bool includeTotalCount,
            CancellationToken cancellationToken);
    }
}
