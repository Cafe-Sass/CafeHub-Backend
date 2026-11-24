using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Wifi.DTOs;

namespace CafeHub.Application.Wifi.Commands
{
    public sealed record DeactivateWifiPackageCommand(Guid PackageId, string ConcurrencyToken)
        : ICommand<WifiPackageResponse>, ITenantTransactionalCommand;

    public sealed record ReactivateWifiPackageCommand(Guid PackageId, string ConcurrencyToken)
        : ICommand<WifiPackageResponse>, ITenantTransactionalCommand;
}
