using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Domain.Identity;

namespace CafeHub.Application.Identity.Commands
{
    public sealed class LogoutTenantUserCommandHandler : ICommandHandler<LogoutTenantUserCommand>
    {
        private readonly ITenantRefreshTokenService refreshTokenService;

        public LogoutTenantUserCommandHandler(ITenantRefreshTokenService refreshTokenService)
        {
            this.refreshTokenService = refreshTokenService ?? throw new ArgumentNullException(nameof(refreshTokenService));
        }

        public async Task<Result> Handle(LogoutTenantUserCommand command, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);
            await refreshTokenService.RevokeFamilyAsync(command.RefreshToken, RefreshTokenRevocationReasons.Logout, cancellationToken);
            return Result.Success();
        }
    }
}
