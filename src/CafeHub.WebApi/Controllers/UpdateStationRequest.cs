namespace CafeHub.WebApi.Controllers
{
    /// <summary>
    /// Request body for updating a station. Kept in WebApi so the application command
    /// stays free of the tenant slug.
    /// </summary>
    public sealed record UpdateStationRequest(string Name, int SortOrder, string ConcurrencyToken);
}
