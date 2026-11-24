namespace CafeHub.Application.Wifi.Contracts
{
    public sealed record IssuedWifiListPage(
        IReadOnlyList<IssuedWifiEntry> Entries,
        int? TotalCount);
}
