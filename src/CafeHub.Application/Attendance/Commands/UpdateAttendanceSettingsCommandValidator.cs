using CafeHub.Domain.Tenants;
using FluentValidation;

namespace CafeHub.Application.Attendance.Commands
{
    public sealed class UpdateAttendanceSettingsCommandValidator
        : AbstractValidator<UpdateAttendanceSettingsCommand>
    {
        public UpdateAttendanceSettingsCommandValidator()
        {
            RuleFor(command => command.Mode)
                .Must(AttendanceSettings.IsValidMode)
                .WithMessage(AttendanceErrors.InvalidMode().Message)
                .WithErrorCode("Attendance.InvalidMode");
        }
    }
}
