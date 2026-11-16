namespace CafeHub.WebApi.Controllers
{
    /// <summary>
    /// Request body for bulk-adding tables to a branch. Kept in WebApi so the application command
    /// stays free of the tenant slug.
    /// </summary>
    public sealed record AddTablesBulkRequest(int Count);
}
