namespace CafeHub.WebApi.Controllers
{
    /// <summary>
    /// Request body for updating a table's label. Kept in WebApi so the application command
    /// stays free of the tenant slug.
    /// </summary>
    public sealed record UpdateTableRequest(string? Label, string ConcurrencyToken);
}
