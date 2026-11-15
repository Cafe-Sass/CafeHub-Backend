using CafeHub.Application.Abstractions.Messaging;

namespace CafeHub.Application.Staff.Commands
{
    public sealed record ChangePasswordCommand(
        string CurrentPassword,
        string NewPassword) : ICommand, ITenantTransactionalCommand;
}
