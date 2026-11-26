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

        public static Error BranchRequired() =>
            Error.Validation("Attendance.BranchRequired", "The branch id is required.");

        public static Error InvalidToken() =>
            Error.Validation("Attendance.InvalidToken", "The attendance token is not valid.");

        public static Error InvalidDateRange() =>
            Error.Validation("Attendance.InvalidDateRange", "The date range is invalid.");

        public static Error ConcurrencyConflict() =>
            Error.Conflict(
                "Attendance.ConcurrencyConflict",
                "The attendance settings were modified. Retry the change.");

        public static Error DeviceNotFound() =>
            Error.NotFound("Attendance.DeviceNotFound", "The attendance device was not found.");

        public static Error StaffNotFound() =>
            Error.NotFound("Attendance.StaffNotFound", "The staff member was not found.");

        public static Error DeviceCodeNotFound() =>
            Error.NotFound("Attendance.DeviceCodeNotFound", "The device code mapping was not found.");

        public static Error DeviceCodeConflict() =>
            Error.Conflict(
                "Attendance.DeviceCodeConflict",
                "The device code conflicts with an existing device code mapping.");

        public static Error DeviceUnauthorized() =>
            Error.Unauthorized(
                "Attendance.DeviceUnauthorized", "The attendance device key is not valid.");

        public static Error DeviceModeNotEnabled() =>
            Error.Conflict(
                "Attendance.DeviceModeNotEnabled", "Attendance device events are accepted only in Device mode.");

        public static Error DeviceEventConflict() =>
            Error.Conflict(
                "Attendance.DeviceEventConflict", "The device event conflicts with an existing event.");
    }
}
