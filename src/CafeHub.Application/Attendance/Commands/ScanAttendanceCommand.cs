using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Attendance.DTOs;

namespace CafeHub.Application.Attendance.Commands
{
    public sealed record ScanAttendanceCommand(string Token) : ICommand<AttendanceScanResponse>;
}
