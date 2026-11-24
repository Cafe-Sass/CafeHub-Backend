using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Wifi.Contracts;
using CafeHub.Application.Wifi.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Wifi.Commands
{
    public sealed class WifiPackageLifecycleCommandHandler(
        IWifiPackageStore packages,
        IWifiSettingsStore wifiSettings,
        ICurrentTenantUser currentUser)
        : ICommandHandler<DeactivateWifiPackageCommand, WifiPackageResponse>,
            ICommandHandler<ReactivateWifiPackageCommand, WifiPackageResponse>
    {
        public Task<Result<WifiPackageResponse>> Handle(
            DeactivateWifiPackageCommand command,
            CancellationToken cancellationToken) =>
            ChangeStatusAsync(
                command.PackageId,
                command.ConcurrencyToken,
                WifiPackageStatus.Inactive,
                cancellationToken);

        public Task<Result<WifiPackageResponse>> Handle(
            ReactivateWifiPackageCommand command,
            CancellationToken cancellationToken) =>
            ChangeStatusAsync(
                command.PackageId,
                command.ConcurrencyToken,
                WifiPackageStatus.Active,
                cancellationToken);

        private async Task<Result<WifiPackageResponse>> ChangeStatusAsync(
            Guid packageId,
            string concurrencyToken,
            WifiPackageStatus status,
            CancellationToken cancellationToken)
        {
            if (currentUser.UserId is not { } actorId || actorId == Guid.Empty)
            {
                return Result.Failure<WifiPackageResponse>(AuthErrors.Forbidden());
            }

            var settings = await wifiSettings.GetAsync(null, cancellationToken);
            if (settings.Policy != WifiPolicy.Paid)
            {
                return Result.Failure<WifiPackageResponse>(WifiErrors.PackagesRequirePaidPolicy());
            }

            var existing = await packages.GetAsync(packageId, cancellationToken);
            if (existing is null)
            {
                return Result.Failure<WifiPackageResponse>(WifiErrors.PackageNotFound());
            }

            if (status == WifiPackageStatus.Active
                && await packages.ActiveNameExistsAsync(existing.Name, packageId, cancellationToken))
            {
                return Result.Failure<WifiPackageResponse>(WifiErrors.PackageNameConflict());
            }

            return await packages.ChangeStatusAsync(
                packageId,
                status,
                concurrencyToken,
                actorId,
                cancellationToken);
        }
    }
}
