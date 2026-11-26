using CafeHub.Domain.Tenants;
using FluentValidation;

namespace CafeHub.Application.Attendance.Commands
{
    public sealed class ScanAttendanceCommandValidator : AbstractValidator<ScanAttendanceCommand>
    {
        public ScanAttendanceCommandValidator()
        {
            RuleFor(command => command.Token)
                .Must(AttendanceQrSession.IsValidToken)
                .WithMessage(AttendanceErrors.InvalidToken().Message)
                .WithErrorCode("Attendance.InvalidToken");
        }
    }
}
