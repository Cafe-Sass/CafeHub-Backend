using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Attendance.Contracts;
using CafeHub.Application.Identity;

namespace CafeHub.Application.Attendance.Commands
{
    public sealed class UnmapAttendanceDeviceCodeCommandHandler(
        IAttendanceDeviceStore store,
        ICurrentTenantUser currentUser)
        : ICommandHandler<UnmapAttendanceDeviceCodeCommand>
    {
        public Task<Result> Handle(
            UnmapAttendanceDeviceCodeCommand command,
            CancellationToken cancellationToken) =>
            AttendanceCommandScope.RunAsync(
                currentUser,
                command.BranchId,
                actorId => store.UnmapCodeAsync(
                    command.BranchId,
                    command.StaffId,
                    actorId,
                    cancellationToken));
    }
}
