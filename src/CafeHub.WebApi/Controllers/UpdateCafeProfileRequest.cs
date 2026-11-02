namespace CafeHub.WebApi.Controllers
{
    /// <summary>
    /// Request body for updating a tenant's cafe profile. Kept in WebApi so the application command
    /// stays free of the tenant slug.
    /// </summary>
    public sealed record UpdateCafeProfileRequest(
        string CafeName,
        string? LogoMediaReference,
        string? PrimaryColor,
        string? SecondaryColor,
        string? AccentColor,
        string? ContactPhone,
        string? ContactEmail,
        string? ContactAddress,
        string Timezone,
        string ConcurrencyToken);
}
