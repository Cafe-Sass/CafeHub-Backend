namespace CafeHub.Application.Wifi.DTOs
{
    public sealed record GuestWifiPackage(
        string Name,
        int DurationMinutes,
        decimal? SpeedMbps,
        string DataMode,
        int? QuotaMb,
        decimal Price);
}
