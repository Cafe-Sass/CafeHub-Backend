namespace CafeHub.Application.Wifi.DTOs
{
    public sealed record GuestWifiNetwork(string Ssid, string Security, string? Password);
}
