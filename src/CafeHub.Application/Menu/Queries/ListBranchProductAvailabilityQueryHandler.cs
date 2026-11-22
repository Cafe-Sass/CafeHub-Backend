using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Menu.Contracts;
using CafeHub.Application.Menu.DTOs;
using CafeHub.Application.Pagination;
using CafeHub.Application.Tenancy;

namespace CafeHub.Application.Menu.Queries
{
    public sealed class ListBranchProductAvailabilityQueryHandler(
        IBranchProductAvailabilityStore store,
        ICurrentTenantUser currentUser,
        ITenantContextAccessor tenantContext,
        PaginationOptions paginationOptions)
        : IQueryHandler<ListBranchProductAvailabilityQuery, OffsetPage<BranchProductAvailabilityItem>>
    {
        public Task<Result<OffsetPage<BranchProductAvailabilityItem>>> Handle(
            ListBranchProductAvailabilityQuery request, CancellationToken cancellationToken)
        {
            var error = ProductActor.CheckBranch(currentUser, tenantContext, request.BranchId, out _);
            return error is null
                ? store.ListAsync(
                    request.BranchId,
                    request.Available,
                    request.CategoryId,
                    request.Page,
                    paginationOptions.ResolvePageSize(request.PageSize),
                    request.IncludeTotalCount,
                    cancellationToken)
                : Task.FromResult(Result.Failure<OffsetPage<BranchProductAvailabilityItem>>(error));
        }
    }
}
