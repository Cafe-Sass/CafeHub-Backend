using CafeHub.Application.Abstractions.Messaging;

namespace CafeHub.Application.Staff.Commands
{
    public sealed record ReactivateStaffCommand(Guid StaffId) : ICommand, ITenantTransactionalCommand;
}
