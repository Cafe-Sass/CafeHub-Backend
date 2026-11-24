using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Wifi.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Wifi.Commands
{
    public sealed record CreateWifiPackageCommand(
        string? Name,
        int DurationMinutes,
        decimal? SpeedMbps,
        WifiDataMode DataMode,
        int? QuotaMb,
        decimal Price,
        int SortOrder) : ICommand<WifiPackageResponse>, ITenantTransactionalCommand;
}
