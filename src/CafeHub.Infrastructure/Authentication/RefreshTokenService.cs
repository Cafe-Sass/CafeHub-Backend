using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CafeHub.Infrastructure.Authentication
{
    internal sealed class RefreshTokenService : IRefreshTokenService
    {
        public const string InvalidRefreshTokenCode = "Auth.InvalidRefreshToken";
        public const string InvalidRefreshTokenMessage = "The refresh token is not valid.";
        public const int MaxRawTokenLength = 512;

        private readonly IRefreshTokenStore store;
        private readonly IMasterUnitOfWork unitOfWork;
        private readonly IRefreshTokenGenerator tokenGenerator;
        private readonly ITokenHasher tokenHasher;
        private readonly RefreshTokenOptions options;
        private readonly TimeProvider timeProvider;
        private readonly ILogger<RefreshTokenService> logger;

        public RefreshTokenService(
            IRefreshTokenStore store,
            IMasterUnitOfWork unitOfWork,
            IRefreshTokenGenerator tokenGenerator,
            ITokenHasher tokenHasher,
            RefreshTokenOptions options,
            TimeProvider timeProvider,
            ILogger<RefreshTokenService> logger)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            this.tokenGenerator = tokenGenerator ?? throw new ArgumentNullException(nameof(tokenGenerator));
            this.tokenHasher = tokenHasher ?? throw new ArgumentNullException(nameof(tokenHasher));
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Result<IssuedRefreshToken>> IssueAsync(Guid adminId, CancellationToken cancellationToken)
        {
            if (adminId == Guid.Empty)
            {
                throw new ArgumentException("The administrator id is required.", nameof(adminId));
            }

            var now = timeProvider.GetUtcNow();
            var expiresAt = now.AddDays(options.LifetimeDays);
            var rawToken = tokenGenerator.Generate();
            var token = RefreshToken.Issue(
                Guid.CreateVersion7(),
                adminId,
                Guid.CreateVersion7(),
                tokenHasher.Hash(rawToken),
                now,
                expiresAt);

            await unitOfWork.BeginTransactionAsync(cancellationToken);
            store.Add(token);
            await unitOfWork.CommitTransactionAsync(cancellationToken);

            return Result.Success(new IssuedRefreshToken
            {
                RawToken = rawToken,
                ExpiresAt = expiresAt,
                FamilyId = token.FamilyId,
            });
        }

        public async Task<Result<RotatedRefreshToken>> RotateAsync(string rawToken, CancellationToken cancellationToken)
        {
            if (!IsPlausibleRawToken(rawToken))
            {
                LogRejection("invalid_input");
                return InvalidRefreshTokenResult<RotatedRefreshToken>();
            }

            var tokenHash = tokenHasher.Hash(rawToken);
            var now = timeProvider.GetUtcNow();

            await unitOfWork.BeginTransactionAsync(cancellationToken);
            var token = await store.FindByTokenHashAsync(tokenHash, cancellationToken);

            if (token is null)
            {
                await unitOfWork.RollbackTransactionAsync(cancellationToken);
                LogRejection("unknown_token");
                return InvalidRefreshTokenResult<RotatedRefreshToken>();
            }

            if (token.RevokedAt is not null)
            {
                await unitOfWork.RollbackTransactionAsync(cancellationToken);
                LogRejection("revoked_token");
                return InvalidRefreshTokenResult<RotatedRefreshToken>();
            }

            if (token.ExpiresAt <= now)
            {
                await unitOfWork.RollbackTransactionAsync(cancellationToken);
                LogRejection("expired_token");
                return InvalidRefreshTokenResult<RotatedRefreshToken>();
            }

            if (token.RotatedAt is not null)
            {
                await RevokeFamilyAndCommitAsync(token.FamilyId, now, RefreshTokenRevocationReasons.ReuseDetected, cancellationToken);
                LogRejection("reuse_detected");
                return InvalidRefreshTokenResult<RotatedRefreshToken>();
            }

            var replacementRawToken = tokenGenerator.Generate();
            var replacement = RefreshToken.Issue(
                Guid.CreateVersion7(),
                token.AdminId,
                token.FamilyId,
                tokenHasher.Hash(replacementRawToken),
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
                await unitOfWork.RollbackTransactionAsync(cancellationToken);
                await unitOfWork.BeginTransactionAsync(cancellationToken);
                await RevokeFamilyAndCommitAsync(token.FamilyId, timeProvider.GetUtcNow(), RefreshTokenRevocationReasons.ReuseDetected, cancellationToken);
                LogRejection("reuse_detected");
                return InvalidRefreshTokenResult<RotatedRefreshToken>();
            }

            return Result.Success(new RotatedRefreshToken
            {
                RawToken = replacementRawToken,
                ExpiresAt = replacement.ExpiresAt,
                AdminId = token.AdminId,
            });
        }

        public async Task<Result> RevokeFamilyAsync(string rawToken, string reason, CancellationToken cancellationToken)
        {
            if (!RefreshTokenRevocationReasons.IsDefined(reason))
            {
                throw new ArgumentException("The revocation reason must be one of the documented reasons.", nameof(reason));
            }

            if (!IsPlausibleRawToken(rawToken))
            {
                LogRejection("invalid_input");
                return Result.Failure(InvalidRefreshTokenError());
            }

            var tokenHash = tokenHasher.Hash(rawToken);
            var now = timeProvider.GetUtcNow();

            await unitOfWork.BeginTransactionAsync(cancellationToken);
            var token = await store.FindByTokenHashAsync(tokenHash, cancellationToken);

            if (token is null)
            {
                await unitOfWork.RollbackTransactionAsync(cancellationToken);
                LogRejection("unknown_token");
                return Result.Failure(InvalidRefreshTokenError());
            }

            await RevokeFamilyAndCommitAsync(token.FamilyId, now, reason, cancellationToken);

            return Result.Success();
        }

        private async Task RevokeFamilyAndCommitAsync(Guid familyId, DateTimeOffset revokedAt, string reason, CancellationToken cancellationToken)
        {
            var family = await store.FindFamilyAsync(familyId, cancellationToken);
            foreach (var familyToken in family)
            {
                familyToken.Revoke(revokedAt, reason);
            }

            await unitOfWork.CommitTransactionAsync(cancellationToken);
        }

        private static bool IsPlausibleRawToken(string? rawToken)
        {
            return !string.IsNullOrWhiteSpace(rawToken) && rawToken.Length <= MaxRawTokenLength;
        }

        private static Error InvalidRefreshTokenError()
        {
            return Error.Unauthorized(InvalidRefreshTokenCode, InvalidRefreshTokenMessage);
        }

        private static Result<T> InvalidRefreshTokenResult<T>()
        {
            return Result.Failure<T>(InvalidRefreshTokenError());
        }

        private void LogRejection(string failureCategory)
        {
            logger.LogWarning("Refresh token request rejected. Failure category: {FailureCategory}", failureCategory);
        }
    }
}
