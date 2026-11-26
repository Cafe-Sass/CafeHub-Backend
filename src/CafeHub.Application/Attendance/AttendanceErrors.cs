using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Attendance
{
    public static class AttendanceErrors
    {
        public static Error SettingsNotFound() =>
            Error.NotFound("Attendance.SettingsNotFound", "The attendance settings were not found.");

        public static Error InvalidMode() =>
            Error.Validation("Attendance.InvalidMode", "The attendance mode must be Off, Qr, or Device.");

        public static Error QrModeNotEnabled() =>
            Error.Conflict(
                "Attendance.QrModeNotEnabled", "Attendance QR sessions are available only in Qr mode.");

        public static Error QrSessionConflict() =>
            Error.Conflict("Attendance.QrSessionConflict", "The attendance QR token is already in use.");

        public static Error BranchNotFound() =>
            Error.NotFound("Attendance.BranchNotFound", "The branch was not found.");

        public static Error ConcurrencyConflict() =>
            Error.Conflict(
                "Attendance.ConcurrencyConflict",
                "The attendance settings were modified. Retry the change.");
    }
}
