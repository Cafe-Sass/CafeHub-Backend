using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Tenants.Domain
{
    public sealed class TenantRefreshTokenTests
    {
        private static readonly DateTimeOffset IssuedAt = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

        private const string TokenHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        private static TenantRefreshToken IssueToken(DateTimeOffset? expiresAt = null)
        {
            return TenantRefreshToken.Issue(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                TokenHash,
                IssuedAt,
                expiresAt ?? IssuedAt.AddDays(7));
        }

        [Fact]
        public void Issue_SetsTheState_AndTheTokenIsActiveUntilExpiry()
        {
            var token = IssueToken();

            Assert.Null(token.RotatedAt);
            Assert.Null(token.RevokedAt);
            Assert.True(token.IsActive(IssuedAt));
            Assert.True(token.IsActive(IssuedAt.AddDays(6)));
            Assert.False(token.IsActive(IssuedAt.AddDays(7)));
        }

        [Fact]
        public void Issue_RejectsExpiryThatIsNotAfterIssue()
        {
            Assert.Throws<ArgumentException>(() => IssueToken(IssuedAt));
            Assert.Throws<ArgumentException>(() => IssueToken(IssuedAt.AddSeconds(-1)));
        }

        [Fact]
        public void Rotate_SetsTheSuccessor_AndRejectsAnotherRotation()
        {
            var token = IssueToken();
            var successorId = Guid.NewGuid();
            var rotatedAt = IssuedAt.AddHours(1);

            token.Rotate(rotatedAt, successorId);

            Assert.Equal(rotatedAt, token.RotatedAt);
            Assert.Equal(successorId, token.ReplacedById);
            Assert.False(token.IsActive(rotatedAt));
            Assert.Throws<InvalidOperationException>(() => token.Rotate(rotatedAt.AddHours(1), Guid.NewGuid()));
        }

        [Fact]
        public void Rotate_RejectsRevokedAndSelfReplacement()
        {
            var revoked = IssueToken();
            revoked.Revoke(IssuedAt.AddMinutes(1), "logout");
            Assert.Throws<InvalidOperationException>(() => revoked.Rotate(IssuedAt.AddMinutes(2), Guid.NewGuid()));

            var token = IssueToken();
            Assert.Throws<ArgumentException>(() => token.Rotate(IssuedAt.AddMinutes(1), token.Id));
        }

        [Fact]
        public void Revoke_RequiresAReason_AndRecordsTheFirstRevocation()
        {
            var token = IssueToken();

            Assert.Throws<ArgumentException>(() => token.Revoke(IssuedAt, " "));
            token.Revoke(IssuedAt.AddHours(1), "reuse_detected");
            token.Revoke(IssuedAt.AddHours(2), "logout");

            Assert.Equal(IssuedAt.AddHours(1), token.RevokedAt);
            Assert.Equal("reuse_detected", token.RevokedReason);
            Assert.False(token.IsActive(IssuedAt.AddHours(1)));
        }
    }
}
