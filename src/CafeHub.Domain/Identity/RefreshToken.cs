using System.Globalization;

namespace CafeHub.Domain.Identity
{
    public sealed class RefreshToken
    {
        private RefreshToken()
        {
        }

        public Guid Id { get; private set; }

        public Guid AdminId { get; private set; }

        public Guid FamilyId { get; private set; }

        public string TokenHash { get; private set; } = string.Empty;

        public DateTimeOffset IssuedAt { get; private set; }

        public DateTimeOffset ExpiresAt { get; private set; }

        public DateTimeOffset? RotatedAt { get; private set; }

        public Guid? ReplacedById { get; private set; }

        public DateTimeOffset? RevokedAt { get; private set; }

        public string? RevokedReason { get; private set; }

        public bool IsActive(DateTimeOffset now)
        {
            return RotatedAt is null && RevokedAt is null && ExpiresAt > now;
        }

        public static RefreshToken Issue(Guid id, Guid adminId, Guid familyId, string tokenHash, DateTimeOffset issuedAt, DateTimeOffset expiresAt)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The refresh token id is required.", nameof(id));
            }

            if (adminId == Guid.Empty)
            {
                throw new ArgumentException("The refresh token administrator id is required.", nameof(adminId));
            }

            if (familyId == Guid.Empty)
            {
                throw new ArgumentException("The refresh token family id is required.", nameof(familyId));
            }

            if (!IsWellFormedTokenHash(tokenHash))
            {
                throw new ArgumentException("The refresh token hash must be the lowercase hex SHA-256 encoding of the raw token.", nameof(tokenHash));
            }

            if (expiresAt <= issuedAt)
            {
                throw new ArgumentException("The refresh token expiry must be after the issue time.", nameof(expiresAt));
            }

            return new RefreshToken
            {
                Id = id,
                AdminId = adminId,
                FamilyId = familyId,
                TokenHash = tokenHash,
                IssuedAt = issuedAt,
                ExpiresAt = expiresAt,
            };
        }

        public void Rotate(DateTimeOffset rotatedAt, Guid replacedById)
        {
            if (RotatedAt is not null)
            {
                throw new InvalidOperationException("A rotated refresh token cannot be rotated again.");
            }

            if (RevokedAt is not null)
            {
                throw new InvalidOperationException("A revoked refresh token cannot be rotated.");
            }

            if (replacedById == Guid.Empty || replacedById == Id)
            {
                throw new ArgumentException("The replacement token id must be a different refresh token.", nameof(replacedById));
            }

            RotatedAt = rotatedAt;
            ReplacedById = replacedById;
        }

        public void Revoke(DateTimeOffset revokedAt, string reason)
        {
            if (RevokedAt is not null)
            {
                return;
            }

            if (!RefreshTokenRevocationReasons.IsDefined(reason))
            {
                throw new ArgumentException("The refresh token revocation reason must be one of the documented reasons.", nameof(reason));
            }

            RevokedAt = revokedAt;
            RevokedReason = reason;
        }

        private static bool IsWellFormedTokenHash(string? tokenHash)
        {
            if (tokenHash is null || tokenHash.Length != 64)
            {
                return false;
            }

            return tokenHash.All(character =>
                char.IsBetween(character, '0', '9') || char.IsBetween(character, 'a', 'f'));
        }
    }
}
