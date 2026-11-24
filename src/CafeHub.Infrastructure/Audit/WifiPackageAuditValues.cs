using System.Globalization;
using CafeHub.Domain.Tenants;

namespace CafeHub.Infrastructure.Audit
{
    internal sealed record WifiPackageAuditValues(
        string Name,
        int DurationMinutes,
        decimal? SpeedMbps,
        WifiDataMode DataMode,
        int? QuotaMb,
        decimal Price,
        int SortOrder)
    {
        public static WifiPackageAuditValues From(WifiPackage package)
        {
            ArgumentNullException.ThrowIfNull(package);
            return new WifiPackageAuditValues(
                package.Name,
                package.DurationMinutes,
                package.SpeedMbps,
                package.DataMode,
                package.QuotaMb,
                package.Price,
                package.SortOrder);
        }

        public string DurationMinutesText => DurationMinutes.ToString(CultureInfo.InvariantCulture);

        public string? SpeedMbpsText => SpeedMbps?.ToString(CultureInfo.InvariantCulture);

        public string? QuotaMbText => QuotaMb?.ToString(CultureInfo.InvariantCulture);

        public string PriceText => Price.ToString(CultureInfo.InvariantCulture);

        public string SortOrderText => SortOrder.ToString(CultureInfo.InvariantCulture);
    }
}
