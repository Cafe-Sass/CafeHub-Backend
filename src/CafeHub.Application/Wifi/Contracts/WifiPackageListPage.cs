namespace CafeHub.Application.Wifi.Contracts
{
    public sealed record WifiPackageListPage(
        IReadOnlyList<WifiPackageListEntry> Entries,
        int? TotalCount);
}
