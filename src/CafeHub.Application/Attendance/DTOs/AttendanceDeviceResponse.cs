using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Attendance.DTOs
{
    public sealed record AttendanceDeviceResponse(
        Guid Id,
        Guid BranchId,
        string Name,
        string Status,
        DateTimeOffset? LastSeenAt,
        DateTimeOffset CreatedAt)
    {
        public static AttendanceDeviceResponse From(AttendanceDevice device) =>
            new(
                device.Id,
                device.BranchId,
                device.Name,
                device.Status.ToString(),
                device.LastSeenAt,
                device.CreatedAt);
    }

    public sealed record AttendanceDeviceCreatedResponse(
        Guid Id,
        Guid BranchId,
        string Name,
        string Status,
        DateTimeOffset CreatedAt,
        string Key);

    public sealed record AttendanceDeviceCodeResponse(
        Guid BranchId,
        Guid StaffId,
        string DeviceCode)
    {
        public static AttendanceDeviceCodeResponse From(AttendanceDeviceCode code) =>
            new(code.BranchId, code.StaffId, code.DeviceCode);
    }
}
