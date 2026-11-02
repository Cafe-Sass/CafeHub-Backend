using CafeHub.Application.CafeProfile.DTOs;

namespace CafeHub.Application.CafeProfile.Contracts
{
    /// <summary>
    /// Read-only persistence port for a tenant's cafe settings. The tenant is resolved from the
    /// write-once tenant context; the reader never accepts a tenant identifier from callers.
    /// </summary>
    public interface ICafeSettingsReader
    {
        Task<CafeProfileResponse?> GetAsync(CancellationToken cancellationToken);
    }
}
