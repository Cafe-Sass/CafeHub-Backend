using CafeHub.Application.Menu.DTOs;
using CafeHub.Application.Pagination;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Menu.Contracts
{
    /// <summary>
    /// Persistence port for the guest menu of one branch. <paramref name="branchId"/> comes from the resolved
    /// table QR context; the store never accepts a tenant identifier from callers.
    /// </summary>
    public interface IGuestMenuStore
    {
        Task<OffsetPage<GuestMenuItemResponse>> ListAsync(
            Guid branchId,
            string? search,
            Guid? categoryId,
            ProductKind? kind,
            bool? caffeine,
            string? tag,
            int page,
            int pageSize,
            IReadOnlyList<SortTerm> sort,
            bool includeTotalCount,
            CancellationToken cancellationToken);
    }
}
