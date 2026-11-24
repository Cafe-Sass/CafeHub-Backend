using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Wifi.DTOs;

namespace CafeHub.Application.Wifi.Commands
{
    public sealed record RegenerateWifiQrCommand(Guid BranchId)
        : ICommand<WifiQrData>, ITenantTransactionalCommand, ISensitiveRequest;
}
