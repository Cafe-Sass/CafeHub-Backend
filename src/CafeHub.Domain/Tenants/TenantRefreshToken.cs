namespace CafeHub.Domain.Tenants
{
    public sealed class TenantRefreshToken
    {
        private TenantRefreshToken()
        {
        }

        public Guid Id { get; private set; }

        public Guid UserId { get; private set; }

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

        public static TenantRefreshToken Issue(
            Guid id,
            Guid userId,
            Guid familyId,
            string tokenHash,
            DateTimeOffset issuedAt,
            DateTimeOffset expiresAt)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The tenant refresh token id is required.", nameof(id));
            }

            if (userId == Guid.Empty)
            {
                throw new ArgumentException("The tenant refresh token user id is required.", nameof(userId));
            }

            if (familyId == Guid.Empty)
            {
                throw new ArgumentException("The tenant refresh token family id is required.", nameof(familyId));
            }

            if (!IsWellFormedTokenHash(tokenHash))
            {
                throw new ArgumentException("The tenant refresh token hash must be lowercase hex SHA-256.", nameof(tokenHash));
            }

            if (expiresAt <= issuedAt)
            {
                throw new ArgumentException("The tenant refresh token expiry must be after the issue time.", nameof(expiresAt));
            }

            return new TenantRefreshToken
            {
                Id = id,
                UserId = userId,
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
                throw new InvalidOperationException("A rotated tenant refresh token cannot be rotated again.");
            }

            if (RevokedAt is not null)
            {
                throw new InvalidOperationException("A revoked tenant refresh token cannot be rotated.");
            }

            if (replacedById == Guid.Empty || replacedById == Id)
            {
                throw new ArgumentException("The replacement tenant token id must be different.", nameof(replacedById));
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

            if (reason is not ("logout" or "reuse_detected" or "admin_revoked"))
            {
                throw new ArgumentException("The tenant refresh token revocation reason is not documented.", nameof(reason));
            }

            RevokedAt = revokedAt;
            RevokedReason = reason;
        }

        private static bool IsWellFormedTokenHash(string? tokenHash)
        {
            if (tokenHash is not { Length: 64 })
            {
                return false;
            }

            foreach (var character in tokenHash)
            {
                if (!char.IsBetween(character, '0', '9') && !char.IsBetween(character, 'a', 'f'))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
