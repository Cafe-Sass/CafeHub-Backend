using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Attendance.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Attendance.Commands
{
    public sealed record UpdateAttendanceSettingsCommand(
        AttendanceMode Mode,
        string ConcurrencyToken) : ICommand<AttendanceSettingsResponse>, ITenantTransactionalCommand;
}
