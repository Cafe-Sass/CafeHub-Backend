using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Domain.Identity;
using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
        private readonly ILogger<TenantRefreshTokenService> logger;

        public TenantRefreshTokenService(ITenantRefreshTokenStore store, ITenantUnitOfWork unitOfWork, IRefreshTokenGenerator generator, ITokenHasher hasher, RefreshTokenOptions options, TimeProvider timeProvider, ILogger<TenantRefreshTokenService> logger)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            this.generator = generator ?? throw new ArgumentNullException(nameof(generator));
            this.hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Result<IssuedTenantRefreshToken>> IssueAsync(Guid userId, CancellationToken cancellationToken)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("The tenant user id is required.", nameof(userId));
            }

            var now = timeProvider.GetUtcNow();
            var raw = generator.Generate();
            var expires = now.AddDays(options.LifetimeDays);
            await unitOfWork.BeginTransactionAsync(cancellationToken);
            var familyId = Guid.CreateVersion7();
            store.Add(TenantRefreshToken.Issue(Guid.CreateVersion7(), userId, familyId, hasher.Hash(raw), now, expires));
            await unitOfWork.CommitTransactionAsync(cancellationToken);
            return Result.Success(new IssuedTenantRefreshToken { RawToken = raw, ExpiresAt = expires, FamilyId = familyId });
        }

        public async Task<Result<RotatedTenantRefreshToken>> RotateAsync(string rawToken, CancellationToken cancellationToken)
        {
            if (!IsPlausibleRawToken(rawToken))
            {
                LogRejection("invalid_input");
                return InvalidRefreshTokenResult<RotatedTenantRefreshToken>();
            }

            var tokenHash = hasher.Hash(rawToken);
            var now = timeProvider.GetUtcNow();
            await unitOfWork.BeginTransactionAsync(cancellationToken);
            var token = await store.FindByTokenHashAsync(tokenHash, cancellationToken);

            if (token is null)
            {
                await unitOfWork.RollbackTransactionAsync(cancellationToken);
                LogRejection("unknown_token");
                return InvalidRefreshTokenResult<RotatedTenantRefreshToken>();
            }

            if (token.RevokedAt is not null)
            {
                await unitOfWork.RollbackTransactionAsync(cancellationToken);
                LogRejection("revoked_token");
                return InvalidRefreshTokenResult<RotatedTenantRefreshToken>();
            }

            if (token.ExpiresAt <= now)
            {
                await unitOfWork.RollbackTransactionAsync(cancellationToken);
                LogRejection("expired_token");
                return InvalidRefreshTokenResult<RotatedTenantRefreshToken>();
            }

            if (token.RotatedAt is not null)
            {
                await RevokeFamilyAndCommitAsync(token.FamilyId, now, RefreshTokenRevocationReasons.ReuseDetected, cancellationToken);
                LogRejection("reuse_detected");
                return InvalidRefreshTokenResult<RotatedTenantRefreshToken>();
            }

            var replacementRawToken = generator.Generate();
            var replacement = TenantRefreshToken.Issue(
                Guid.CreateVersion7(),
                token.UserId,
                token.FamilyId,
                hasher.Hash(replacementRawToken),
                now,
                now.AddDays(options.LifetimeDays));
            token.Rotate(now, replacement.Id);
            store.Add(replacement);

            try
            {
                await unitOfWork.CommitTransactionAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                var familyId = token.FamilyId;
                await unitOfWork.RollbackTransactionAsync(cancellationToken);
                store.ClearTracking();
                await unitOfWork.BeginTransactionAsync(cancellationToken);
                await RevokeFamilyAndCommitAsync(familyId, timeProvider.GetUtcNow(), RefreshTokenRevocationReasons.ReuseDetected, cancellationToken);
                LogRejection("reuse_detected");
                return InvalidRefreshTokenResult<RotatedTenantRefreshToken>();
            }

            return Result.Success(new RotatedTenantRefreshToken
            {
                RawToken = replacementRawToken,
                ExpiresAt = replacement.ExpiresAt,
                UserId = token.UserId,
            });
        }

        public async Task<Result<Guid>> RevokeFamilyAsync(string rawToken, string reason, CancellationToken cancellationToken)
        {
            if (!RefreshTokenRevocationReasons.IsDefined(reason))
            {
                throw new ArgumentException("The revocation reason must be one of the documented reasons.", nameof(reason));
            }

            if (!IsPlausibleRawToken(rawToken))
            {
                LogRejection("invalid_input");
                return InvalidRefreshTokenResult<Guid>();
            }

            var tokenHash = hasher.Hash(rawToken);
            await unitOfWork.BeginTransactionAsync(cancellationToken);
            var token = await store.FindByTokenHashAsync(tokenHash, cancellationToken);

            if (token is null)
            {
                await unitOfWork.RollbackTransactionAsync(cancellationToken);
                LogRejection("unknown_token");
                return InvalidRefreshTokenResult<Guid>();
            }

            await RevokeFamilyAndCommitAsync(token.FamilyId, timeProvider.GetUtcNow(), reason, cancellationToken);
            return Result.Success(token.UserId);
        }

        private async Task RevokeFamilyAndCommitAsync(Guid familyId, DateTimeOffset revokedAt, string reason, CancellationToken cancellationToken)
        {
            var family = await store.FindFamilyAsync(familyId, cancellationToken);
            foreach (var token in family)
            {
                token.Revoke(revokedAt, reason);
            }

            await unitOfWork.CommitTransactionAsync(cancellationToken);
        }

        private static bool IsPlausibleRawToken(string? rawToken)
        {
            return !string.IsNullOrWhiteSpace(rawToken) && rawToken.Length <= ITenantRefreshTokenService.MaxRawTokenLength;
        }

        private static Result<T> InvalidRefreshTokenResult<T>()
        {
            return Result.Failure<T>(AuthErrors.InvalidRefreshToken());
        }

        private void LogRejection(string category)
        {
            logger.LogWarning("Tenant refresh token request rejected. Failure category: {FailureCategory}", category);
        }
    }
}
