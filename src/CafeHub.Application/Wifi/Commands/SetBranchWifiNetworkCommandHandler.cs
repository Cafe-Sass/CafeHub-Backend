using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Wifi.Contracts;
using CafeHub.Application.Wifi.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Wifi.Commands
{
    public sealed class SetBranchWifiNetworkCommandHandler(
        IWifiSettingsStore store,
        ICurrentTenantUser currentUser)
        : ICommandHandler<SetBranchWifiNetworkCommand, WifiNetworkResponse?>
    {
        public async Task<Result<WifiNetworkResponse?>> Handle(
            SetBranchWifiNetworkCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<WifiNetworkResponse?>(scope.Error!);
            }

            if (scope.Value.HasValue && scope.Value != command.BranchId)
            {
                return Result.Failure<WifiNetworkResponse?>(WifiErrors.NotFound());
            }

            if (currentUser.UserId is not { } actorId || actorId == Guid.Empty)
            {
                return Result.Failure<WifiNetworkResponse?>(AuthErrors.Forbidden());
            }

            return await store.SetNetworkAsync(
                scope.Value,
                command.BranchId,
                command.Ssid,
                command.Security,
                command.Password,
                actorId,
                cancellationToken);
        }
    }
}
