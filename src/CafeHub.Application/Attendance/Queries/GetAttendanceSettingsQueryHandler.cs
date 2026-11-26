using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Attendance.Contracts;
using CafeHub.Application.Attendance.DTOs;

namespace CafeHub.Application.Attendance.Queries
{
    public sealed class GetAttendanceSettingsQueryHandler(IAttendanceSettingsReader reader)
        : IQueryHandler<GetAttendanceSettingsQuery, AttendanceSettingsResponse>
    {
        public async Task<Result<AttendanceSettingsResponse>> Handle(
            GetAttendanceSettingsQuery query, CancellationToken cancellationToken)
        {
            var settings = await reader.GetAsync(cancellationToken);
            return settings is null
                ? Result.Failure<AttendanceSettingsResponse>(AttendanceErrors.SettingsNotFound())
                : Result.Success(settings);
        }
    }
}
