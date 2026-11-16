namespace CafeHub.WebApi.Controllers
{
    /// <summary>
    /// Request body for creating a station. Kept in WebApi so the application command
    /// stays free of the tenant slug.
    /// </summary>
    public sealed record CreateStationRequest(string Name, int SortOrder);
}
