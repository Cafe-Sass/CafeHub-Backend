using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Wifi.DTOs;

namespace CafeHub.Application.Wifi.Commands
{
    public sealed record ImportWifiVouchersCommand(Guid PackageId, IReadOnlyList<string?>? Codes)
        : ICommand<WifiVoucherImportResult>, ITenantTransactionalCommand, ISensitiveRequest;

    public sealed record VoidWifiVoucherCommand(Guid PackageId, string? Code)
        : ICommand, ITenantTransactionalCommand, ISensitiveRequest;
}
