namespace CafeHub.WebApi.Controllers
{
    /// <summary>
    /// Request body for adding a table to a branch. Kept in WebApi so the application command
    /// stays free of the tenant slug.
    /// </summary>
    public sealed record AddTableRequest(string? Label);
}
