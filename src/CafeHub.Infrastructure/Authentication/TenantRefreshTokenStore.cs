using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Authentication
{
    internal sealed class TenantRefreshTokenStore(TenantDbContext context) : ITenantRefreshTokenStore
    {
        public Task<TenantRefreshToken?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
        {
            return context.TenantRefreshTokens.SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);
        }

        public async Task<IReadOnlyList<TenantRefreshToken>> FindFamilyAsync(Guid familyId, CancellationToken cancellationToken)
        {
            return await context.TenantRefreshTokens.Where(token => token.FamilyId == familyId).ToListAsync(cancellationToken);
        }

        public void Add(TenantRefreshToken token) => context.TenantRefreshTokens.Add(token);

        public void ClearTracking() => context.ChangeTracker.Clear();
    }
}
