namespace CafeHub.WebApi.Controllers
{
    /// <summary>
    /// Request body for replacing a staff member's station assignments.
    /// Kept in WebApi so the application command stays free of the tenant slug.
    /// </summary>
    public sealed record SetStaffStationsRequest(IReadOnlyList<Guid>? StationIds);
}
