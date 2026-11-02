using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.CafeProfile.DTOs;

namespace CafeHub.Application.CafeProfile.Commands
{
    /// <summary>
    /// Updates the signed-in tenant's cafe profile. Marked as a tenant transactional command so the
    /// profile update and the tenant audit entry commit together in one tenant transaction.
    /// </summary>
    public sealed record UpdateCafeProfileCommand(
        string CafeName,
        string? LogoMediaReference,
        string? PrimaryColor,
        string? SecondaryColor,
        string? AccentColor,
        string? ContactPhone,
        string? ContactEmail,
        string? ContactAddress,
        string Timezone,
        string ConcurrencyToken) : ICommand<CafeProfileResponse>, ITenantTransactionalCommand;
}
