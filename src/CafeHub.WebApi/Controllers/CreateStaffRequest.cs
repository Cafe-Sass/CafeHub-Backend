namespace CafeHub.WebApi.Controllers
{
    /// <summary>
    /// Request body for creating a tenant staff member.
    /// </summary>
    public sealed record CreateStaffRequest(
        string FullName,
        string Email,
        string? PhoneNumber,
        Guid BranchId,
        string Password);
}
