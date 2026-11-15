namespace CafeHub.WebApi.Controllers
{
    /// <summary>
    /// Request body for resetting a staff member's password (owner-initiated).
    /// </summary>
    public sealed record ResetStaffPasswordRequest(string NewPassword);
}
