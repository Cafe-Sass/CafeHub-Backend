namespace CafeHub.Application.Wifi.Contracts
{
    public sealed record IssuedWifiEntry(
        Guid PackageId,
        string PackageName,
        int DurationMinutes,
        decimal? SpeedMbps,
        int OrderNumber,
        string OrderReference,
        DateTimeOffset? IssuedAt,
        string? Code,
        decimal OrderTotal,
        decimal OrderPaid,
        decimal BillTotal,
        decimal BillPaid);
}
