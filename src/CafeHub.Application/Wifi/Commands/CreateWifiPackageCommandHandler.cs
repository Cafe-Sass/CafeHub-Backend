using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Wifi.Contracts;
using CafeHub.Application.Wifi.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Wifi.Commands
{
    public sealed class CreateWifiPackageCommandHandler(
        IWifiPackageStore packages,
        IWifiSettingsStore wifiSettings,
        ICurrentTenantUser currentUser,
        TimeProvider timeProvider)
        : ICommandHandler<CreateWifiPackageCommand, WifiPackageResponse>
    {
        public async Task<Result<WifiPackageResponse>> Handle(
            CreateWifiPackageCommand command,
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

            var name = command.Name?.Trim() ?? string.Empty;
            if (await packages.ActiveNameExistsAsync(name, null, cancellationToken))
            {
                return Result.Failure<WifiPackageResponse>(WifiErrors.PackageNameConflict());
            }

            var package = WifiPackage.Create(
                Guid.CreateVersion7(),
                name,
                command.DurationMinutes,
                command.SpeedMbps,
                command.DataMode,
                command.QuotaMb,
                command.Price,
                command.SortOrder,
                timeProvider.GetUtcNow(),
                actorId);
            return await packages.CreateAsync(package, cancellationToken);
        }
    }
}
