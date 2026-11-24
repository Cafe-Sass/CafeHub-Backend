using CafeHub.Application.Wifi.Contracts;

namespace CafeHub.Application.Wifi.DTOs
{
    public sealed record WifiPackageListItem(
        Guid Id,
        string Name,
        int DurationMinutes,
        decimal? SpeedMbps,
        string DataMode,
        int? QuotaMb,
        decimal Price,
        int SortOrder,
        string Status)
    {
        public static WifiPackageListItem From(WifiPackageListEntry entry) =>
            new(
                entry.Id,
                entry.Name,
                entry.DurationMinutes,
                entry.SpeedMbps,
                entry.DataMode.ToString(),
                entry.QuotaMb,
                entry.Price,
                entry.SortOrder,
                entry.Status.ToString());
    }
}
