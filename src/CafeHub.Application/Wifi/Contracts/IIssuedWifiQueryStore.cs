namespace CafeHub.Application.Wifi.Contracts
{
    /// <summary>
    /// Read port for the staff issued-WiFi lookup. Rows are package order lines with their voucher
    /// state; the caller's branch scope is applied, and status is derived from the assigned voucher
    /// and the confirmed payments rather than stored.
    /// </summary>
    public interface IIssuedWifiQueryStore
    {
        Task<IssuedWifiListPage> ListAsync(
            IssuedWifiListRequest request,
            CancellationToken cancellationToken);
    }
}
