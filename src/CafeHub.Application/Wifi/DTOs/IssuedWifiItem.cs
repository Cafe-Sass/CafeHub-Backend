using CafeHub.Application.Wifi.Contracts;

namespace CafeHub.Application.Wifi.DTOs
{
    public sealed record IssuedWifiItem(
        Guid PackageId,
        string PackageName,
        int DurationMinutes,
        decimal? SpeedMbps,
        int OrderNumber,
        string OrderReference,
        DateTimeOffset? IssuedAt,
        string? Code,
        string Status)
    {
        public static IssuedWifiItem From(IssuedWifiEntry entry)
        {
            ArgumentNullException.ThrowIfNull(entry);
            var released = WifiCodeRelease.IsReleased(
                entry.OrderTotal,
                entry.OrderPaid,
                entry.BillTotal,
                entry.BillPaid);
            var status = entry.Code is null
                ? IssuedWifiStatus.AwaitingVoucher
                : released
                    ? IssuedWifiStatus.Released
                    : IssuedWifiStatus.AwaitingPayment;
            return new IssuedWifiItem(
                entry.PackageId,
                entry.PackageName,
                entry.DurationMinutes,
                entry.SpeedMbps,
                entry.OrderNumber,
                entry.OrderReference,
                entry.IssuedAt,
                entry.Code,
                status.ToString());
        }
    }
}
