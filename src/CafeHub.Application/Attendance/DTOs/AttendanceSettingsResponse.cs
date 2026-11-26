using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Attendance.DTOs
{
    public sealed record AttendanceSettingsResponse(string Mode, string ConcurrencyToken)
    {
        public static AttendanceSettingsResponse From(
            AttendanceSettings settings, string concurrencyToken) =>
            new(settings.Mode.ToString(), concurrencyToken);
    }
}
