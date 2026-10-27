using CafeHub.Domain.Tenants;

namespace CafeHub.Infrastructure.Authentication
{
    internal interface ITenantRefreshTokenStore
    {
        Task<TenantRefreshToken?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

        Task<IReadOnlyList<TenantRefreshToken>> FindFamilyAsync(Guid familyId, CancellationToken cancellationToken);

        void Add(TenantRefreshToken token);

        void ClearTracking();
    }
}
