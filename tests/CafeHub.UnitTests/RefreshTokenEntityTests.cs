using CafeHub.Domain.Identity;

namespace CafeHub.UnitTests
{
    public sealed class RefreshTokenEntityTests
    {
        private static readonly DateTimeOffset IssuedAt = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

        private const string TokenHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        private static RefreshToken IssueToken(DateTimeOffset? expiresAt = null)
        {
            return RefreshToken.Issue(
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

            Assert.False(token.RotatedAt.HasValue);
            Assert.False(token.RevokedAt.HasValue);
            Assert.True(token.IsActive(IssuedAt));
            Assert.True(token.IsActive(IssuedAt.AddDays(6)));
            Assert.False(token.IsActive(IssuedAt.AddDays(7)));
        }

        [Fact]
        public void Issue_Throws_WhenExpiryIsNotAfterIssue()
        {
            Assert.Throws<ArgumentException>(() => RefreshToken.Issue(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TokenHash, IssuedAt, IssuedAt));
            Assert.Throws<ArgumentException>(() => RefreshToken.Issue(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TokenHash, IssuedAt, IssuedAt.AddSeconds(-1)));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        [InlineData("0123456789abcdef0123456789abcdef")]
        [InlineData("0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF")]
        [InlineData("0123456789abcdeg0123456789abcdef0123456789abcdef0123456789abcdef")]
        public void Issue_Throws_WhenTheTokenHashIsNotLowercaseHexSha256(string? invalidHash)
        {
            Assert.Throws<ArgumentException>(() =>
                RefreshToken.Issue(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), invalidHash!, IssuedAt, IssuedAt.AddDays(7)));
        }

        [Fact]
        public void Issue_Throws_WhenAnIdentifierIsEmpty()
        {
            Assert.Throws<ArgumentException>(() => RefreshToken.Issue(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), TokenHash, IssuedAt, IssuedAt.AddDays(7)));
            Assert.Throws<ArgumentException>(() => RefreshToken.Issue(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), TokenHash, IssuedAt, IssuedAt.AddDays(7)));
            Assert.Throws<ArgumentException>(() => RefreshToken.Issue(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, TokenHash, IssuedAt, IssuedAt.AddDays(7)));
        }

        [Fact]
        public void Rotate_MarksTheTokenRotated_AndPointsAtTheSuccessor()
        {
            var token = IssueToken();
            var rotatedAt = IssuedAt.AddDays(1);
            var successorId = Guid.NewGuid();

            token.Rotate(rotatedAt, successorId);

            Assert.Equal(rotatedAt, token.RotatedAt);
            Assert.Equal(successorId, token.ReplacedById);
            Assert.False(token.IsActive(rotatedAt));
        }

        [Fact]
        public void Rotate_Throws_WhenTheTokenIsAlreadyRotated()
        {
            var token = IssueToken();
            token.Rotate(IssuedAt.AddDays(1), Guid.NewGuid());

            var exception = Record.Exception(() => token.Rotate(IssuedAt.AddDays(2), Guid.NewGuid()));

            Assert.IsType<InvalidOperationException>(exception);
        }

        [Fact]
        public void Rotate_Throws_WhenTheTokenIsRevoked()
        {
            var token = IssueToken();
            token.Revoke(IssuedAt.AddHours(1), RefreshTokenRevocationReasons.AdminRevoked);

            var exception = Record.Exception(() => token.Rotate(IssuedAt.AddDays(1), Guid.NewGuid()));

            Assert.IsType<InvalidOperationException>(exception);
        }

        [Fact]
        public void Rotate_Throws_WhenTheTokenReplacesItself()
        {
            var token = IssueToken();

            Assert.Throws<ArgumentException>(() => token.Rotate(IssuedAt.AddDays(1), token.Id));
        }

        [Fact]
        public void Revoke_SetsTheRevocationTime_AndTheReason()
        {
            var token = IssueToken();
            var revokedAt = IssuedAt.AddHours(2);

            token.Revoke(revokedAt, RefreshTokenRevocationReasons.AdminRevoked);

            Assert.Equal(revokedAt, token.RevokedAt);
            Assert.Equal(RefreshTokenRevocationReasons.AdminRevoked, token.RevokedReason);
            Assert.False(token.IsActive(revokedAt));
        }

        [Fact]
        public void Revoke_IsIdempotent_AndKeepsTheFirstReason()
        {
            var token = IssueToken();
            token.Revoke(IssuedAt.AddHours(2), RefreshTokenRevocationReasons.Logout);

            token.Revoke(IssuedAt.AddHours(3), RefreshTokenRevocationReasons.ReuseDetected);

            Assert.Equal(IssuedAt.AddHours(2), token.RevokedAt);
            Assert.Equal(RefreshTokenRevocationReasons.Logout, token.RevokedReason);
        }

        [Fact]
        public void Revoke_Throws_WhenTheReasonIsNotADocumentedReason()
        {
            var token = IssueToken();

            Assert.Throws<ArgumentException>(() => token.Revoke(IssuedAt, "hacked"));
            Assert.Throws<ArgumentException>(() => token.Revoke(IssuedAt, ""));
        }

        [Fact]
        public void Revoke_AppliesToARotatedToken()
        {
            var token = IssueToken();
            token.Rotate(IssuedAt.AddHours(1), Guid.NewGuid());

            token.Revoke(IssuedAt.AddHours(2), RefreshTokenRevocationReasons.ReuseDetected);

            Assert.NotNull(token.RevokedAt);
            Assert.Equal(RefreshTokenRevocationReasons.ReuseDetected, token.RevokedReason);
        }
    }
}
