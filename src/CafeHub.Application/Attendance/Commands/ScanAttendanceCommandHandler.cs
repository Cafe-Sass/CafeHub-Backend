using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Attendance.Contracts;
using CafeHub.Application.Attendance.DTOs;
using CafeHub.Application.Identity;

namespace CafeHub.Application.Attendance.Commands
{
    public sealed class ScanAttendanceCommandHandler(
        IAttendanceRecordStore store,
        ICurrentTenantUser currentUser)
        : ICommandHandler<ScanAttendanceCommand, AttendanceScanResponse>
    {
        public async Task<Result<AttendanceScanResponse>> Handle(
            ScanAttendanceCommand command, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            if (currentUser.UserId is not { } actorId || actorId == Guid.Empty)
            {
                return Result.Failure<AttendanceScanResponse>(AuthErrors.Forbidden());
            }

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure || scope.Value is not { } staffBranchId)
            {
                return Result.Failure<AttendanceScanResponse>(AuthErrors.Forbidden());
            }

            return await store.ScanAsync(actorId, staffBranchId, command.Token, cancellationToken);
        }
    }
}
