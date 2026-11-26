using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Attendance.Contracts;
using CafeHub.Application.Attendance.DTOs;
using CafeHub.Application.Identity;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Attendance.Commands
{
    public sealed class IssueAttendanceQrCommandHandler(
        IAttendanceQrSessionStore store,
        IAttendanceSettingsReader settingsReader,
        ICurrentTenantUser currentUser)
        : ICommandHandler<IssueAttendanceQrCommand, AttendanceQrSessionResponse>
    {
        public async Task<Result<AttendanceQrSessionResponse>> Handle(
            IssueAttendanceQrCommand command, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<AttendanceQrSessionResponse>(scope.Error!);
            }

            if (scope.Value is { } scopedBranchId && scopedBranchId != command.BranchId)
            {
                return Result.Failure<AttendanceQrSessionResponse>(AttendanceErrors.BranchNotFound());
            }

            if (currentUser.UserId is not { } actorId || actorId == Guid.Empty)
            {
                return Result.Failure<AttendanceQrSessionResponse>(AuthErrors.Forbidden());
            }

            var settings = await settingsReader.GetAsync(cancellationToken);
            if (settings is null
                || !string.Equals(
                    settings.Mode, AttendanceMode.Qr.ToString(), StringComparison.Ordinal))
            {
                return Result.Failure<AttendanceQrSessionResponse>(AttendanceErrors.QrModeNotEnabled());
            }

            return await store.IssueAsync(command.BranchId, actorId, cancellationToken);
        }
    }
}
