namespace CafeHub.WebApi.Controllers
{
    /// <summary>
    /// Request body for branch lifecycle actions (suspend, reactivate, retire). Only the
    /// concurrency token is required; the target status is implied by the action route.
    /// </summary>
    public sealed record BranchLifecycleRequest(string ConcurrencyToken);
}
