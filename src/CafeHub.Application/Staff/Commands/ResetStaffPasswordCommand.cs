using CafeHub.Application.Abstractions.Messaging;

namespace CafeHub.Application.Staff.Commands
{
    public sealed record ResetStaffPasswordCommand(
        Guid StaffId,
        string NewPassword) : ICommand, ITenantTransactionalCommand;
}
