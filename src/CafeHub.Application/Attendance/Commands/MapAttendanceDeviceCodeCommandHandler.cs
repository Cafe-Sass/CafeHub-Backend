using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Attendance.Contracts;
using CafeHub.Application.Attendance.DTOs;
using CafeHub.Application.Identity;

namespace CafeHub.Application.Attendance.Commands
{
    public sealed class MapAttendanceDeviceCodeCommandHandler(
        IAttendanceDeviceStore store,
        ICurrentTenantUser currentUser)
        : ICommandHandler<MapAttendanceDeviceCodeCommand, AttendanceDeviceCodeResponse>
    {
        public Task<Result<AttendanceDeviceCodeResponse>> Handle(
            MapAttendanceDeviceCodeCommand command,
            CancellationToken cancellationToken) =>
            AttendanceCommandScope.RunAsync(
                currentUser,
                command.BranchId,
                actorId => store.MapCodeAsync(
                    command.BranchId,
                    command.StaffId,
                    command.DeviceCode!,
                    actorId,
                    cancellationToken));
    }
}
