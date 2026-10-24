using CafeHub.Domain.Identity;

namespace CafeHub.Infrastructure.Authentication
{
    internal interface IRefreshTokenStore
    {
        Task<RefreshToken?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

        Task<IReadOnlyList<RefreshToken>> FindFamilyAsync(Guid familyId, CancellationToken cancellationToken);

        void Add(RefreshToken token);
    }
}
