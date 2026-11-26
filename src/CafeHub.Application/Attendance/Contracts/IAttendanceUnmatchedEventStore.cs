using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Attendance.DTOs;

namespace CafeHub.Application.Attendance.Contracts
{
    public interface IAttendanceUnmatchedEventStore
    {
        Task<IReadOnlyList<AttendanceUnmatchedEventResponse>> ListAsync(
            Guid branchId,
            CancellationToken cancellationToken);

        Task<Result<AttendanceDeviceReprocessResponse>> ReprocessAsync(
            Guid branchId,
            Guid actorUserId,
            CancellationToken cancellationToken);
    }
}
