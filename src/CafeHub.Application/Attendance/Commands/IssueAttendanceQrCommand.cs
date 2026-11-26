using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Attendance.DTOs;

namespace CafeHub.Application.Attendance.Commands
{
    public sealed record IssueAttendanceQrCommand(Guid BranchId)
        : ICommand<AttendanceQrSessionResponse>;
}
