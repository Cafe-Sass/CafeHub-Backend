namespace CafeHub.WebApi.Controllers
{
    public sealed record UpdateWifiSettingsRequest(string? Policy, string? ConcurrencyToken);

    public sealed record SetBranchWifiNetworkRequest(string? Ssid, string? Security, string? Password);

    public sealed record CreateWifiPackageRequest(
        string? Name,
        int DurationMinutes,
        decimal? SpeedMbps,
        string? DataMode,
        int? QuotaMb,
        decimal Price,
        int SortOrder);

    public sealed record UpdateWifiPackageRequest(
        string? Name,
        int DurationMinutes,
        decimal? SpeedMbps,
        string? DataMode,
        int? QuotaMb,
        decimal Price,
        int SortOrder,
        string? ConcurrencyToken);

    public sealed record WifiPackageLifecycleRequest(string? ConcurrencyToken);

    public sealed record ImportWifiVouchersRequest(IReadOnlyList<string?>? Codes);

    public sealed record VoidWifiVoucherRequest(string? Code);

    public sealed record SubmitWifiOrderRequest(Guid PackageId, string? GuestDisplayName);
}
