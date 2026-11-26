using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Attendance.DTOs
{
    public sealed record AttendanceQrSessionResponse(
        Guid BranchId,
        string Token,
        DateTimeOffset ValidFrom,
        DateTimeOffset ValidTo)
    {
        public static AttendanceQrSessionResponse From(AttendanceQrSession session) =>
            new(session.BranchId, session.Token, session.ValidFrom, session.ValidTo);
    }
}
