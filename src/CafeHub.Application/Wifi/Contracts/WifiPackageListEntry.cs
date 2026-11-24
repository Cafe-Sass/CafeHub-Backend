using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Wifi.Contracts
{
    public sealed record WifiPackageListEntry(
        Guid Id,
        string Name,
        int DurationMinutes,
        decimal? SpeedMbps,
        WifiDataMode DataMode,
        int? QuotaMb,
        decimal Price,
        int SortOrder,
        WifiPackageStatus Status);
}
