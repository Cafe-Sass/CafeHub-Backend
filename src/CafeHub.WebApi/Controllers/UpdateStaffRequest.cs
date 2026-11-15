namespace CafeHub.WebApi.Controllers
{
    /// <summary>
    /// Request body for updating a tenant staff member profile.
    /// </summary>
    public sealed record UpdateStaffRequest(
        string FullName,
        string? PhoneNumber,
        Guid BranchId,
        string ConcurrencyToken);
}
