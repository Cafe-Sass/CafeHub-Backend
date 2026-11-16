using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Menu.DTOs;
using CafeHub.Application.Pagination;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Menu.Contracts
{
    public interface IMenuCategoryStore
    {
        Task<Result<MenuCategoryResponse>> CreateAsync(
            Guid? parentId, string name, int sortOrder, Guid actorId, CancellationToken cancellationToken);

        Task<Result<MenuCategoryResponse>> UpdateAsync(
            Guid id, string name, int sortOrder, uint version, Guid actorId, CancellationToken cancellationToken);

        Task<Result<MenuCategoryResponse>> ChangeStatusAsync(
            Guid id, MenuCategoryStatus status, uint version, Guid actorId, CancellationToken cancellationToken);

        Task<MenuCategoryResponse?> GetVisibleAsync(Guid id, CancellationToken cancellationToken);

        Task<OffsetPage<MenuCategoryTreeNodeResponse>> GetTreeAsync(
            bool includeInactive, int page, int pageSize, bool includeTotalCount, CancellationToken cancellationToken);
    }
}
