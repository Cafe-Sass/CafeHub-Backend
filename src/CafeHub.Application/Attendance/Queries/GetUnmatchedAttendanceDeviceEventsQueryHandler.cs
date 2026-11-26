using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Attendance.Contracts;
using CafeHub.Application.Attendance.DTOs;
using CafeHub.Application.Identity;

namespace CafeHub.Application.Attendance.Queries
{
    public sealed class GetUnmatchedAttendanceDeviceEventsQueryHandler(
        IAttendanceUnmatchedEventStore store,
        ICurrentTenantUser currentUser)
        : IQueryHandler<GetUnmatchedAttendanceDeviceEventsQuery, IReadOnlyList<AttendanceUnmatchedEventResponse>>
    {
        public async Task<Result<IReadOnlyList<AttendanceUnmatchedEventResponse>>> Handle(
            GetUnmatchedAttendanceDeviceEventsQuery query, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<IReadOnlyList<AttendanceUnmatchedEventResponse>>(scope.Error!);
            }

            if (scope.Value.HasValue && query.BranchId.HasValue && query.BranchId != scope.Value)
            {
                return Result.Failure<IReadOnlyList<AttendanceUnmatchedEventResponse>>(
                    AttendanceErrors.BranchNotFound());
            }

            var branchId = scope.Value ?? query.BranchId;
            if (branchId is null)
            {
                return Result.Failure<IReadOnlyList<AttendanceUnmatchedEventResponse>>(
                    AttendanceErrors.BranchRequired());
            }

            return Result.Success(await store.ListAsync(branchId.Value, cancellationToken));
        }
    }
}
