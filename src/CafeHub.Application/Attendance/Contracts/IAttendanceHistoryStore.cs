using CafeHub.Application.Attendance.DTOs;

namespace CafeHub.Application.Attendance.Contracts
{
    public interface IAttendanceHistoryStore
    {
        Task<AttendanceHistoryPage> ListAsync(
            AttendanceHistoryRequest request, CancellationToken cancellationToken);
    }
}
