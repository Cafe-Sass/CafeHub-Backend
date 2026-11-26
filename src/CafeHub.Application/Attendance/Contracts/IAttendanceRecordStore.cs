using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Attendance.DTOs;

namespace CafeHub.Application.Attendance.Contracts
{
    public interface IAttendanceRecordStore
    {
        Task<Result<AttendanceScanResponse>> ScanAsync(
            Guid actorUserId,
            Guid staffBranchId,
            string token,
            CancellationToken cancellationToken);
    }
}
