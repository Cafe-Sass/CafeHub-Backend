using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Attendance.Contracts;
using CafeHub.Application.Attendance.DTOs;

namespace CafeHub.Application.Attendance.Commands
{
    public sealed class IngestAttendanceDeviceEventsCommandHandler(
        IAttendanceDeviceEventStore store,
        TimeProvider timeProvider)
        : ICommandHandler<IngestAttendanceDeviceEventsCommand, AttendanceDeviceEventsResponse>
    {
        public Task<Result<AttendanceDeviceEventsResponse>> Handle(
            IngestAttendanceDeviceEventsCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            return store.IngestAsync(
                command.DeviceId,
                command.BranchId,
                command.Events!,
                timeProvider.GetUtcNow(),
                cancellationToken);
        }
    }
}
