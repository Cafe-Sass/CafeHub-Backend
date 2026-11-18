using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Menu.DTOs;
using CafeHub.Application.Pagination;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Menu.Contracts
{
    public interface IProductStore
    {
        Task<Result<ProductResponse>> CreateAsync(
            ProductWriteModel product, Guid actorId, CancellationToken cancellationToken);

        Task<Result<ProductResponse>> UpdateAsync(
            Guid id, ProductWriteModel product, uint version, Guid actorId, CancellationToken cancellationToken);

        Task<Result<ProductResponse>> ChangeStatusAsync(
            Guid id, ProductStatus status, uint version, Guid actorId, CancellationToken cancellationToken,
            bool includeHiddenCategories = false);

        Task<Result<OffsetPage<ProductListItemResponse>>> ListAsync(
            Guid? categoryId,
            ProductKind? kind,
            bool? caffeine,
            string? tag,
            ProductStatus? status,
            string? search,
            int page,
            int pageSize,
            IReadOnlyList<SortTerm> sort,
            bool includeTotalCount,
            CancellationToken cancellationToken,
            bool includeHiddenCategories = false);

        Task<Result<ProductResponse>> GetAsync(Guid id, CancellationToken cancellationToken, bool includeHiddenCategories = false);
    }
}
