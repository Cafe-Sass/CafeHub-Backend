namespace CafeHub.WebApi.Controllers
{
    public sealed record CreateAttendanceDeviceRequest(Guid BranchId, string? Name);

    public sealed record AttendanceDeviceScopeRequest(Guid BranchId);

    public sealed record MapAttendanceDeviceCodeRequest(Guid BranchId, Guid StaffId, string? DeviceCode);
}
