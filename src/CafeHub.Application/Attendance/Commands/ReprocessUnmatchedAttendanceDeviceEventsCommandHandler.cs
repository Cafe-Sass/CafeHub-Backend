using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Attendance.Contracts;
using CafeHub.Application.Attendance.DTOs;
using CafeHub.Application.Identity;

namespace CafeHub.Application.Attendance.Commands
{
    public sealed class ReprocessUnmatchedAttendanceDeviceEventsCommandHandler(
        IAttendanceUnmatchedEventStore store,
        ICurrentTenantUser currentUser)
        : ICommandHandler<ReprocessUnmatchedAttendanceDeviceEventsCommand, AttendanceDeviceReprocessResponse>
    {
        public Task<Result<AttendanceDeviceReprocessResponse>> Handle(
            ReprocessUnmatchedAttendanceDeviceEventsCommand command,
            CancellationToken cancellationToken) =>
            AttendanceCommandScope.RunAsync(
                currentUser,
                command.BranchId,
                actorId => store.ReprocessAsync(command.BranchId, actorId, cancellationToken));
    }
}
