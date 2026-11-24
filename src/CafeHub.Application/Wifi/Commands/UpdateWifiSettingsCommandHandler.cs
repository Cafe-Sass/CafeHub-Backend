using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Wifi.Contracts;
using CafeHub.Application.Wifi.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Wifi.Commands
{
    public sealed class UpdateWifiSettingsCommandHandler(
        IWifiSettingsStore store,
        ICurrentTenantUser currentUser)
        : ICommandHandler<UpdateWifiSettingsCommand, WifiSettingsResponse>
    {
        public async Task<Result<WifiSettingsResponse>> Handle(
            UpdateWifiSettingsCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<WifiSettingsResponse>(scope.Error!);
            }

            if (currentUser.UserId is not { } actorId || actorId == Guid.Empty)
            {
                return Result.Failure<WifiSettingsResponse>(AuthErrors.Forbidden());
            }

            return await store.UpdatePolicyAsync(
                command.Policy,
                command.ConcurrencyToken,
                actorId,
                scope.Value,
                cancellationToken);
        }
    }
}
