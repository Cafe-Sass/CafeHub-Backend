using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Wifi.DTOs;

namespace CafeHub.Application.Wifi.Queries
{
    public sealed record GetWifiSettingsQuery : IQuery<WifiSettingsResponse>;
}
