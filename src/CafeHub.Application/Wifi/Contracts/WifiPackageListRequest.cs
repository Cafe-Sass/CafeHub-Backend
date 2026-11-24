using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Wifi.Contracts
{
    public sealed record WifiPackageListRequest(
        int Page,
        int PageSize,
        bool IncludeTotalCount,
        WifiPackageStatus? Status);
}
