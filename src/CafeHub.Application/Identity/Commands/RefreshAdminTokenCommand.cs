using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Identity.DTOs;

namespace CafeHub.Application.Identity.Commands
{
    public sealed record RefreshAdminTokenCommand(string RefreshToken) : ICommand<AdminTokenResponse>;
}
