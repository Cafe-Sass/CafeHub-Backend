using CafeHub.Application.Attendance.DTOs;

namespace CafeHub.Application.Attendance.Contracts
{
    public interface IAttendanceSettingsReader
    {
        Task<AttendanceSettingsResponse?> GetAsync(CancellationToken cancellationToken);
    }
}
