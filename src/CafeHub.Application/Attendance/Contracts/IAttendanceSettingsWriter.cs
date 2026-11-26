using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Attendance.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Attendance.Contracts
{
    public interface IAttendanceSettingsWriter
    {
        Task<Result<AttendanceSettingsResponse>> UpdateAsync(
            AttendanceMode mode,
            Guid actorUserId,
            string concurrencyToken,
            CancellationToken cancellationToken);
    }
}
