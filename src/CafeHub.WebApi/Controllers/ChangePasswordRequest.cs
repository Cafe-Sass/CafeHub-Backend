namespace CafeHub.WebApi.Controllers
{
    /// <summary>
    /// Request body for changing the currently authenticated staff member's own password.
    /// </summary>
    public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
}
