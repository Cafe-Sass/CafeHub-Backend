using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Identity.DTOs;

namespace CafeHub.Application.Identity.Commands
{
    public sealed record LoginAdminCommand(string Email, string Password) : ICommand<AdminTokenResponse>;
}
