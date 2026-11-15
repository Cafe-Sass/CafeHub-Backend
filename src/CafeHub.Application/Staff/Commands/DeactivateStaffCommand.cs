using CafeHub.Application.Abstractions.Messaging;

namespace CafeHub.Application.Staff.Commands
{
    public sealed record DeactivateStaffCommand(Guid StaffId) : ICommand, ITenantTransactionalCommand;
}
