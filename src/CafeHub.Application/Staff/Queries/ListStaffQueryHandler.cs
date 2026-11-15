using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Pagination;
using CafeHub.Application.Staff.Contracts;
using CafeHub.Application.Staff.DTOs;

namespace CafeHub.Application.Staff.Queries
{
    public sealed class ListStaffQueryHandler : IQueryHandler<ListStaffQuery, OffsetPage<StaffResponse>>
    {
        private readonly IStaffStore staffStore;
        private readonly ICurrentTenantUser currentUser;
        private readonly PaginationOptions paginationOptions;

        public ListStaffQueryHandler(IStaffStore staffStore, PaginationOptions paginationOptions, ICurrentTenantUser currentUser)
        {
            this.staffStore = staffStore ?? throw new ArgumentNullException(nameof(staffStore));
            this.currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            this.paginationOptions = paginationOptions ?? throw new ArgumentNullException(nameof(paginationOptions));
        }

        public async Task<Result<OffsetPage<StaffResponse>>> Handle(ListStaffQuery query, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<OffsetPage<StaffResponse>>(scope.Error!);
            }

            if (scope.Value.HasValue && query.BranchId.HasValue && query.BranchId != scope.Value)
            {
                return Result.Failure<OffsetPage<StaffResponse>>(StaffErrors.NotFound());
            }

            var pageSize = paginationOptions.ResolvePageSize(query.PageSize);

            var listPage = await staffStore.ListAsync(
                new StaffListRequest(query.Page, pageSize, query.IncludeTotalCount, scope.Value ?? query.BranchId, query.IsActive),
                cancellationToken);

            var page = OffsetPage<StaffResponse>.Create(
                listPage.FetchedItems,
                query.Page,
                pageSize,
                listPage.TotalCount);

            return Result.Success(page);
        }
    }
}
