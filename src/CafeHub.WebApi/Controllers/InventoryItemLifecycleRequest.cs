namespace CafeHub.WebApi.Controllers
{
    public sealed record InventoryItemLifecycleRequest(string ConcurrencyToken);
}
