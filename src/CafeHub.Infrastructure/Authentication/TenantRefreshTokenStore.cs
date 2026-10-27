using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;

namespace CafeHub.Infrastructure.Authentication
{
    internal sealed class TenantRefreshTokenStore(TenantDbContext context) : ITenantRefreshTokenStore
    {
        public void Add(TenantRefreshToken token) => context.TenantRefreshTokens.Add(token);
    }
}
