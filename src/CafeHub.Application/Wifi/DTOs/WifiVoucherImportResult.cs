namespace CafeHub.Application.Wifi.DTOs
{
    public sealed record WifiVoucherImportResult(int Added, int Duplicate, int Invalid);
}
