using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Domain.Tenants;

namespace CafeHub.Infrastructure.Authentication
{
    internal sealed class TenantRefreshTokenService : ITenantRefreshTokenService
    {
        private readonly ITenantRefreshTokenStore store;
        private readonly ITenantUnitOfWork unitOfWork;
        private readonly IRefreshTokenGenerator generator;
        private readonly ITokenHasher hasher;
        private readonly RefreshTokenOptions options;
        private readonly TimeProvider timeProvider;

        public TenantRefreshTokenService(ITenantRefreshTokenStore store, ITenantUnitOfWork unitOfWork, IRefreshTokenGenerator generator, ITokenHasher hasher, RefreshTokenOptions options, TimeProvider timeProvider)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            this.generator = generator ?? throw new ArgumentNullException(nameof(generator));
            this.hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        }

        public async Task<Result<IssuedTenantRefreshToken>> IssueAsync(Guid userId, CancellationToken cancellationToken)
        {
            var now = timeProvider.GetUtcNow();
            var raw = generator.Generate();
            var expires = now.AddDays(options.LifetimeDays);
            await unitOfWork.BeginTransactionAsync(cancellationToken);
            var familyId = Guid.CreateVersion7();
            store.Add(TenantRefreshToken.Issue(Guid.CreateVersion7(), userId, familyId, hasher.Hash(raw), now, expires));
            await unitOfWork.CommitTransactionAsync(cancellationToken);
            return Result.Success(new IssuedTenantRefreshToken { RawToken = raw, ExpiresAt = expires, FamilyId = familyId });
        }
    }
}
