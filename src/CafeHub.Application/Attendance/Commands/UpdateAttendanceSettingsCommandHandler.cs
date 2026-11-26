using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Attendance.Contracts;
using CafeHub.Application.Attendance.DTOs;
using CafeHub.Application.Identity;

namespace CafeHub.Application.Attendance.Commands
{
    public sealed class UpdateAttendanceSettingsCommandHandler(
        IAttendanceSettingsWriter writer,
        ICurrentTenantUser currentUser)
        : ICommandHandler<UpdateAttendanceSettingsCommand, AttendanceSettingsResponse>
    {
        public async Task<Result<AttendanceSettingsResponse>> Handle(
            UpdateAttendanceSettingsCommand command, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            if (currentUser.UserId is not { } actorId || actorId == Guid.Empty)
            {
                return Result.Failure<AttendanceSettingsResponse>(AuthErrors.Forbidden());
            }

            return await writer.UpdateAsync(
                command.Mode, actorId, command.ConcurrencyToken, cancellationToken);
        }
    }
}
