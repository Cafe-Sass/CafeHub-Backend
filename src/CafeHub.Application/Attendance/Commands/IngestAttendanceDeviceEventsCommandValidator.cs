using CafeHub.Domain.Tenants;
using FluentValidation;

namespace CafeHub.Application.Attendance.Commands
{
    public sealed class IngestAttendanceDeviceEventsCommandValidator
        : AbstractValidator<IngestAttendanceDeviceEventsCommand>
    {
        public const int MaximumEvents = 100;

        public IngestAttendanceDeviceEventsCommandValidator()
        {
            RuleFor(command => command.Events)
                .NotNull()
                .Must(events => events is { Count: >= 1 and <= MaximumEvents })
                .WithMessage($"A batch must contain between 1 and {MaximumEvents} events.")
                .WithErrorCode("Attendance.InvalidDeviceEvent");
            RuleForEach(command => command.Events)
                .ChildRules(eventRule =>
                {
                    eventRule.RuleFor(item => item.EventId)
                        .Must(AttendanceDeviceEvent.IsValidEventId)
                        .WithMessage(
                            $"The device event id must be 1 to {AttendanceDeviceEvent.MaximumEventIdLength} characters without control characters.")
                        .WithErrorCode("Attendance.InvalidDeviceEvent");
                    eventRule.RuleFor(item => item.Code)
                        .Must(AttendanceDeviceCode.IsValidCode)
                        .WithMessage(
                            $"The device code must be 1 to {AttendanceDeviceCode.MaximumCodeLength} characters without control characters.")
                        .WithErrorCode("Attendance.InvalidDeviceEvent");
                    eventRule.RuleFor(item => item.Direction)
                        .Must(direction => direction is null
                            || (Enum.TryParse<AttendanceDeviceEventDirection>(direction, true, out var parsed)
                                && Enum.IsDefined(parsed)))
                        .WithMessage("The device event direction must be In or Out.")
                        .WithErrorCode("Attendance.InvalidDeviceEvent");
                });
        }
    }
}
