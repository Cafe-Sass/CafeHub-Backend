namespace CafeHub.WebApi.Controllers
{
    /// <summary>
    /// Request body for creating a tenant branch. Kept in WebApi so the application command
    /// stays free of the tenant slug.
    /// </summary>
    public sealed record CreateBranchRequest(
        string Name,
        string? Address,
        string? ContactPhone,
        string? OpeningHoursSummary);
}
