using System.Globalization;
using CafeHub.Application.CafeProfile.Contracts;
using CafeHub.Application.CafeProfile.DTOs;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.CafeProfile
{
    /// <summary>
    /// Reads a tenant's cafe settings through the scoped <see cref="TenantDbContext"/>, exactly like the
    /// existing tenant stores. The tenant is resolved from the write-once tenant context; no tenant
    /// identifier is accepted from callers.
    /// </summary>
    internal sealed class CafeSettingsStore(TenantDbContext context) : ICafeSettingsReader
    {
        public async Task<CafeProfileResponse?> GetAsync(CancellationToken cancellationToken)
        {
            var settings = await context.CafeSettings.FirstOrDefaultAsync(cancellationToken);
            if (settings is null)
            {
                return null;
            }

            var concurrencyToken = context.Entry(settings)
                .Property<uint>("xmin")
                .CurrentValue
                .ToString(CultureInfo.InvariantCulture);

            return CafeProfileResponse.From(settings, concurrencyToken);
        }
    }
}
