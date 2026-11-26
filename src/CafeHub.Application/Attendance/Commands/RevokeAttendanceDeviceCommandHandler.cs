using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Attendance.Contracts;
using CafeHub.Application.Attendance.DTOs;
using CafeHub.Application.Identity;

namespace CafeHub.Application.Attendance.Commands
{
    public sealed class RevokeAttendanceDeviceCommandHandler(
        IAttendanceDeviceStore store,
        ICurrentTenantUser currentUser)
        : ICommandHandler<RevokeAttendanceDeviceCommand, AttendanceDeviceResponse>
    {
        public Task<Result<AttendanceDeviceResponse>> Handle(
            RevokeAttendanceDeviceCommand command,
            CancellationToken cancellationToken) =>
            AttendanceCommandScope.RunAsync(
                currentUser,
                command.BranchId,
                actorId => store.RevokeAsync(
                    command.DeviceId,
                    command.BranchId,
                    actorId,
                    cancellationToken));
    }
}
