using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Attendance.Contracts;
using CafeHub.Application.Attendance.DTOs;
using CafeHub.Application.Identity;
using CafeHub.Application.Printing;

namespace CafeHub.Application.Attendance.Commands
{
    public sealed class CreateAttendanceDeviceCommandHandler(
        IAttendanceDeviceStore store,
        ICurrentTenantUser currentUser)
        : ICommandHandler<CreateAttendanceDeviceCommand, AttendanceDeviceCreatedResponse>
    {
        public Task<Result<AttendanceDeviceCreatedResponse>> Handle(
            CreateAttendanceDeviceCommand command,
            CancellationToken cancellationToken) =>
            AttendanceCommandScope.RunAsync(
                currentUser,
                command.BranchId,
                actorId => CreateAsync(command, actorId, cancellationToken));

        private async Task<Result<AttendanceDeviceCreatedResponse>> CreateAsync(
            CreateAttendanceDeviceCommand command,
            Guid actorId,
            CancellationToken cancellationToken)
        {
            var key = ConnectorKeys.Generate();
            var created = await store.CreateAsync(
                command.BranchId,
                command.Name!,
                ConnectorKeys.Hash(key),
                actorId,
                cancellationToken);
            if (created.IsFailure)
            {
                return Result.Failure<AttendanceDeviceCreatedResponse>(created.Error!);
            }

            var device = created.Value;
            return Result.Success(new AttendanceDeviceCreatedResponse(
                device.Id,
                device.BranchId,
                device.Name,
                device.Status,
                device.CreatedAt,
                key));
        }
    }
}
