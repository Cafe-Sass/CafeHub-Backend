using CafeHub.Application.Abstractions.Messaging;

namespace CafeHub.Application.Identity.Commands
{
    public sealed record LogoutAdminCommand(string RefreshToken) : ICommand;
}
