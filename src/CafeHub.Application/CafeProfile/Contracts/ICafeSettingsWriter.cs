using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.CafeProfile.DTOs;

namespace CafeHub.Application.CafeProfile.Contracts
{
    /// <summary>
    /// Write persistence port for a tenant's cafe settings. The tenant is resolved from the
    /// write-once tenant context; the writer never accepts a tenant identifier from callers.
    /// </summary>
    public interface ICafeSettingsWriter
    {
        Task<Result<CafeProfileResponse>> UpdateAsync(
            string cafeName,
            string? logoMediaReference,
            string? primaryColor,
            string? secondaryColor,
            string? accentColor,
            string? contactPhone,
            string? contactEmail,
            string? contactAddress,
            string timezone,
            Guid actorUserId,
            string concurrencyToken,
            CancellationToken cancellationToken);
    }
}
