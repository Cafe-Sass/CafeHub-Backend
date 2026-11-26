using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Attendance.DTOs;

namespace CafeHub.Application.Attendance.Commands
{
    public sealed record IngestAttendanceDeviceEventsCommand(
        Guid DeviceId,
        Guid BranchId,
        IReadOnlyList<AttendanceDeviceEventInput>? Events)
        : ICommand<AttendanceDeviceEventsResponse>, ITenantTransactionalCommand;
}
