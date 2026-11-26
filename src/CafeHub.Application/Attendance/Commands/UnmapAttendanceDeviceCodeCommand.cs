using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Persistence;

namespace CafeHub.Application.Attendance.Commands
{
    public sealed record UnmapAttendanceDeviceCodeCommand(
        Guid BranchId,
        Guid StaffId) : ICommand, ITenantTransactionalCommand;
}
