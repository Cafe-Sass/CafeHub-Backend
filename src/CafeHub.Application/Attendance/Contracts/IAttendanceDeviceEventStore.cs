using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Attendance.DTOs;

namespace CafeHub.Application.Attendance.Contracts
{
    public interface IAttendanceDeviceEventStore
    {
        Task<Result<AttendanceDeviceEventsResponse>> IngestAsync(
            Guid deviceId,
            Guid branchId,
            IReadOnlyList<AttendanceDeviceEventInput> events,
            DateTimeOffset now,
            CancellationToken cancellationToken);
    }
}
