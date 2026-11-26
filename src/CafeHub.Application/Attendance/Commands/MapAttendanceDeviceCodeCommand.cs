using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Attendance.DTOs;

namespace CafeHub.Application.Attendance.Commands
{
    public sealed record MapAttendanceDeviceCodeCommand(
        Guid BranchId,
        Guid StaffId,
        string? DeviceCode) : ICommand<AttendanceDeviceCodeResponse>, ITenantTransactionalCommand;
}
