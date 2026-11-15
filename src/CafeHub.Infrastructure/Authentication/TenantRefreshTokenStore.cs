using CafeHub.Application.Identity;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Identity;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Authentication
{
    internal sealed class TenantRefreshTokenStore(TenantDbContext context, TenantAccountReader accounts) : ITenantRefreshTokenStore
    {
        public Task LockUserAsync(Guid userId, CancellationToken cancellationToken) =>
            accounts.LockAsync(userId, cancellationToken);

        public Task<TenantAccountAccess?> FindActiveAccountAsync(Guid userId, CancellationToken cancellationToken) =>
            accounts.FindActiveAsync(userId, cancellationToken);

        public Task<Guid?> FindUserIdByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
            context.TenantRefreshTokens.Where(token => token.TokenHash == tokenHash)
                .Select(token => (Guid?)token.UserId)
                .SingleOrDefaultAsync(cancellationToken);

        public Task<TenantRefreshToken?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
        {
            // Rotation and follow-up revocation can run in separate transactions on this same context.
            ClearTracking();
            return context.TenantRefreshTokens.SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);
        }

        public async Task<IReadOnlyList<TenantRefreshToken>> FindFamilyAsync(Guid familyId, CancellationToken cancellationToken)
        {
            return await context.TenantRefreshTokens.Where(token => token.FamilyId == familyId).ToListAsync(cancellationToken);
        }

        public void Add(TenantRefreshToken token) => context.TenantRefreshTokens.Add(token);

        public void ClearTracking()
        {
            foreach (var entry in context.ChangeTracker.Entries<TenantRefreshToken>().ToArray())
            {
                entry.State = EntityState.Detached;
            }
        }
    }
}
