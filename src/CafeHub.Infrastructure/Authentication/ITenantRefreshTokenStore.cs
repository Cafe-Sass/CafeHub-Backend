using CafeHub.Application.Identity;
using CafeHub.Domain.Tenants;

namespace CafeHub.Infrastructure.Authentication
{
    internal interface ITenantRefreshTokenStore
    {
        Task LockUserAsync(Guid userId, CancellationToken cancellationToken);
        Task<TenantAccountAccess?> FindActiveAccountAsync(Guid userId, CancellationToken cancellationToken);
        Task<Guid?> FindUserIdByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);
        Task<TenantRefreshToken?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

        Task<IReadOnlyList<TenantRefreshToken>> FindFamilyAsync(Guid familyId, CancellationToken cancellationToken);

        void Add(TenantRefreshToken token);

        void ClearTracking();
    }
}
