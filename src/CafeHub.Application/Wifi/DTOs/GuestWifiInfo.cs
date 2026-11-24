namespace CafeHub.Application.Wifi.DTOs
{
    /// <summary>
    /// The guest view of a branch WiFi QR: the cafe-level policy and either the free network details
    /// or the Active packages, never both. It carries no internal ids and no staff identity.
    /// </summary>
    public sealed record GuestWifiInfo(
        string Policy,
        GuestWifiNetwork? Network,
        IReadOnlyList<GuestWifiPackage> Packages);
}
