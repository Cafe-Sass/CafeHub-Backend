namespace CafeHub.Application.Inventory.Contracts
{
    public interface IInventoryAlertStore
    {
        Task<InventoryAlertListPage> ListAsync(
            InventoryAlertListRequest request, CancellationToken cancellationToken);
    }
}
