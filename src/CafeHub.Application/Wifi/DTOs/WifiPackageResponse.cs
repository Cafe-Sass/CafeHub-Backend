using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Wifi.DTOs
{
    /// <summary>
    /// One paid WiFi package with its opaque concurrency token. The token guards updates and lifecycle
    /// changes and is not derived from the package name.
    /// </summary>
    public sealed record WifiPackageResponse(
        Guid Id,
        string Name,
        int DurationMinutes,
        decimal? SpeedMbps,
        string DataMode,
        int? QuotaMb,
        decimal Price,
        int SortOrder,
        string Status,
        string ConcurrencyToken)
    {
        public static WifiPackageResponse From(WifiPackage package, string concurrencyToken)
        {
            ArgumentNullException.ThrowIfNull(package);
            ArgumentNullException.ThrowIfNull(concurrencyToken);

            return new WifiPackageResponse(
                package.Id,
                package.Name,
                package.DurationMinutes,
                package.SpeedMbps,
                package.DataMode.ToString(),
                package.QuotaMb,
                package.Price,
                package.SortOrder,
                package.Status.ToString(),
                concurrencyToken);
        }
    }
}
