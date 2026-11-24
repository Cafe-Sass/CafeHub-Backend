using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Wifi.Contracts;
using CafeHub.Application.Wifi.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Wifi.Commands
{
    public sealed class UpdateWifiPackageCommandHandler(
        IWifiPackageStore packages,
        IWifiSettingsStore wifiSettings,
        ICurrentTenantUser currentUser)
        : ICommandHandler<UpdateWifiPackageCommand, WifiPackageResponse>
    {
        public async Task<Result<WifiPackageResponse>> Handle(
            UpdateWifiPackageCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            if (currentUser.UserId is not { } actorId || actorId == Guid.Empty)
            {
                return Result.Failure<WifiPackageResponse>(AuthErrors.Forbidden());
            }

            var settings = await wifiSettings.GetAsync(null, cancellationToken);
            if (settings.Policy != WifiPolicy.Paid)
            {
                return Result.Failure<WifiPackageResponse>(WifiErrors.PackagesRequirePaidPolicy());
            }

            var existing = await packages.GetAsync(command.PackageId, cancellationToken);
            if (existing is null)
            {
                return Result.Failure<WifiPackageResponse>(WifiErrors.PackageNotFound());
            }

            var name = command.Name?.Trim() ?? string.Empty;
            if (existing.Status == WifiPackageStatus.Active.ToString()
                && await packages.ActiveNameExistsAsync(name, command.PackageId, cancellationToken))
            {
                return Result.Failure<WifiPackageResponse>(WifiErrors.PackageNameConflict());
            }

            return await packages.UpdateAsync(
                command.PackageId,
                command.ConcurrencyToken,
                name,
                command.DurationMinutes,
                command.SpeedMbps,
                command.DataMode,
                command.QuotaMb,
                command.Price,
                command.SortOrder,
                actorId,
                cancellationToken);
        }
    }
}
