using CafeHub.Domain.Tenants;

namespace CafeHub.Infrastructure.Authentication
{
    internal interface ITenantRefreshTokenStore
    {
        void Add(TenantRefreshToken token);
    }
}
