using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Attendance
{
    public sealed record AttendanceScanContext(
        AttendanceMode Mode,
        AttendanceQrSession? Session,
        Guid ActorUserId,
        bool ActorIsActive,
        Guid? ActorBranchId,
        AttendanceRecord? OpenRecord,
        bool SessionAlreadyUsed,
        DateTimeOffset Now);
}
