using CafeHub.Domain.Identity;
using CafeHub.Infrastructure.Persistence.Master;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Authentication
{
    internal sealed class RefreshTokenStore(MasterDbContext context) : IRefreshTokenStore
    {
        public Task<RefreshToken?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
        {
            return context.RefreshTokens
                .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);
        }

        public async Task<IReadOnlyList<RefreshToken>> FindFamilyAsync(Guid familyId, CancellationToken cancellationToken)
        {
            return await context.RefreshTokens
                .Where(token => token.FamilyId == familyId)
                .ToListAsync(cancellationToken);
        }

        public void Add(RefreshToken token)
        {
            context.RefreshTokens.Add(token);
        }
    }
}
