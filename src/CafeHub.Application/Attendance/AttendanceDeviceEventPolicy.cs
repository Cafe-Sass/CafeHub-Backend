using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Attendance
{
    public sealed record AttendanceDeviceEventContext(
        Guid DeviceBranchId,
        Guid? StaffId,
        Guid? StaffBranchId,
        bool StaffIsActive,
        AttendanceRecord? OpenRecord,
        DateTimeOffset OccurredAt);

    public enum AttendanceDeviceEventPairing
    {
        CheckIn,
        CheckOut,
        Unmatched,
        Rejected,
    }

    public sealed record AttendanceDeviceEventDecision(
        AttendanceDeviceEventPairing Pairing,
        Guid? RecordId);

    public static class AttendanceDeviceEventPolicy
    {
        public const int FutureToleranceMinutes = 5;
        public const int BackfillDays = 7;

        public static bool IsWithinWindow(DateTimeOffset occurredAt, DateTimeOffset now) =>
            occurredAt >= now.AddDays(-BackfillDays)
            && occurredAt <= now.AddMinutes(FutureToleranceMinutes);

        public static AttendanceDeviceEventDecision Decide(AttendanceDeviceEventContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (context.DeviceBranchId == Guid.Empty
                || context.StaffId is not { } staffId
                || staffId == Guid.Empty
                || !context.StaffIsActive
                || context.StaffBranchId != context.DeviceBranchId)
            {
                return new AttendanceDeviceEventDecision(
                    AttendanceDeviceEventPairing.Unmatched, null);
            }

            if (context.OpenRecord is not { } openRecord)
            {
                return new AttendanceDeviceEventDecision(
                    AttendanceDeviceEventPairing.CheckIn, null);
            }

            if (openRecord.BranchId != context.DeviceBranchId)
            {
                return new AttendanceDeviceEventDecision(
                    AttendanceDeviceEventPairing.Unmatched, null);
            }

            if (context.OccurredAt <= openRecord.CheckInAt)
            {
                return new AttendanceDeviceEventDecision(
                    AttendanceDeviceEventPairing.Rejected, null);
            }

            return new AttendanceDeviceEventDecision(
                AttendanceDeviceEventPairing.CheckOut, openRecord.Id);
        }
    }
}
