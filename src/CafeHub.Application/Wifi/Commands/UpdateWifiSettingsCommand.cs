using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Wifi.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Wifi.Commands
{
    public sealed record UpdateWifiSettingsCommand(WifiPolicy Policy, string ConcurrencyToken)
        : ICommand<WifiSettingsResponse>, ITenantTransactionalCommand;
}
