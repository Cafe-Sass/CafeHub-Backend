namespace CafeHub.WebApi.Controllers
{
    /// <summary>
    /// Request body for station lifecycle actions (deactivate, reactivate). Only the
    /// concurrency token is required; the target status is implied by the action route.
    /// </summary>
    public sealed record StationLifecycleRequest(string ConcurrencyToken);
}
