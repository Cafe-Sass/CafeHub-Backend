using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Attendance.Contracts;
using CafeHub.Application.Attendance.DTOs;
using CafeHub.Application.Identity;
using CafeHub.Application.Pagination;

namespace CafeHub.Application.Attendance.Queries
{
    public sealed class GetAttendanceHistoryQueryHandler(
        IAttendanceHistoryStore store,
        ICurrentTenantUser currentUser,
        PaginationOptions paginationOptions)
        : IQueryHandler<GetAttendanceHistoryQuery, OffsetPage<AttendanceHistoryItem>>
    {
        public async Task<Result<OffsetPage<AttendanceHistoryItem>>> Handle(
            GetAttendanceHistoryQuery query, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<OffsetPage<AttendanceHistoryItem>>(scope.Error!);
            }

            if (scope.Value.HasValue && query.BranchId.HasValue && query.BranchId != scope.Value)
            {
                return Result.Failure<OffsetPage<AttendanceHistoryItem>>(AttendanceErrors.BranchNotFound());
            }

            var branchId = scope.Value ?? query.BranchId;
            if (branchId is null)
            {
                return Result.Failure<OffsetPage<AttendanceHistoryItem>>(AttendanceErrors.BranchRequired());
            }

            var pageSize = paginationOptions.ResolvePageSize(query.PageSize);
            var page = await store.ListAsync(
                new AttendanceHistoryRequest(
                    query.Page,
                    pageSize,
                    query.IncludeTotalCount,
                    branchId.Value,
                    query.StaffId,
                    query.From,
                    query.To),
                cancellationToken);

            return Result.Success(OffsetPage<AttendanceHistoryItem>.Create(
                page.Items, query.Page, pageSize, page.TotalCount));
        }
    }
}
