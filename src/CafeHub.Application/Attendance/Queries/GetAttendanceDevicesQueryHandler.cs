using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Attendance.Contracts;
using CafeHub.Application.Attendance.DTOs;
using CafeHub.Application.Identity;

namespace CafeHub.Application.Attendance.Queries
{
    public sealed class GetAttendanceDevicesQueryHandler(
        IAttendanceDeviceStore store,
        ICurrentTenantUser currentUser)
        : IQueryHandler<GetAttendanceDevicesQuery, IReadOnlyList<AttendanceDeviceResponse>>
    {
        public async Task<Result<IReadOnlyList<AttendanceDeviceResponse>>> Handle(
            GetAttendanceDevicesQuery query, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<IReadOnlyList<AttendanceDeviceResponse>>(scope.Error!);
            }

            if (scope.Value.HasValue && query.BranchId.HasValue && query.BranchId != scope.Value)
            {
                return Result.Failure<IReadOnlyList<AttendanceDeviceResponse>>(
                    AttendanceErrors.BranchNotFound());
            }

            var branchId = scope.Value ?? query.BranchId;
            if (branchId is null)
            {
                return Result.Failure<IReadOnlyList<AttendanceDeviceResponse>>(
                    AttendanceErrors.BranchRequired());
            }

            return Result.Success(await store.ListAsync(branchId.Value, cancellationToken));
        }
    }
}
