namespace CafeHub.Application.Wifi.DTOs
{
    public sealed record WifiVoucherSummary(int Available, int Assigned, int Void, int Total);
}
