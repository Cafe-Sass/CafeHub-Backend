using CafeHub.Domain.Identity;
using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Tenants.Domain
{
    public sealed class TenantUserEmailTests
    {
        [Fact]
        public void Normalize_ReturnsLowercasedTrimmedEmail()
        {
            var result = TenantUserEmail.Normalize("  Owner@Example.COM  ");

            Assert.Equal("owner@example.com", result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Normalize_RejectsEmptyEmail(string? email)
        {
            Assert.Throws<ArgumentException>(() => TenantUserEmail.Normalize(email!));
        }
    }

    public sealed class TenantUserRolesTests
    {
        [Fact]
        public void Owner_ReturnsTheSeededRoleName()
        {
            Assert.Equal("Owner", TenantUserRoles.Owner);
        }
    }

    public sealed class TenantUserTests
    {
        private static readonly Guid UserId = Guid.CreateVersion7();
        private static readonly Guid ActorId = Guid.CreateVersion7();

        private static TenantUser CreateValidUser()
        {
            return TenantUser.Create(
                UserId,
                TenantUserKind.Owner,
                "cafe-owner",
                "cafe-owner",
                "Cafe Owner",
                "owner@example.com",
                "owner@example.com",
                "+1-555-0100",
                "hash-value",
                "security-stamp-value",
                "concurrency-stamp-value",
                new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero),
                ActorId);
        }

        [Fact]
        public void Create_SetsAllProperties()
        {
            var user = CreateValidUser();

            Assert.Equal(UserId, user.Id);
            Assert.Equal(TenantUserKind.Owner, user.UserKind);
            Assert.Equal("cafe-owner", user.UserName);
            Assert.Equal("cafe-owner", user.NormalizedUserName);
            Assert.Equal("Cafe Owner", user.FullName);
            Assert.Equal("owner@example.com", user.Email);
            Assert.Equal("owner@example.com", user.NormalizedEmail);
            Assert.False(user.EmailConfirmed);
            Assert.Equal("+1-555-0100", user.PhoneNumber);
            Assert.False(user.PhoneNumberConfirmed);
            Assert.Equal("hash-value", user.PasswordHash);
            Assert.False(user.TwoFactorEnabled);
            Assert.True(user.LockoutEnabled);
            Assert.Null(user.LockoutEndAt);
            Assert.Equal(0, user.AccessFailedCount);
            Assert.Equal("security-stamp-value", user.SecurityStamp);
            Assert.Equal("concurrency-stamp-value", user.ConcurrencyStamp);
            Assert.Equal(new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero), user.CreatedAt);
            Assert.Equal(new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero), user.UpdatedAt);
            Assert.Equal(ActorId, user.CreatedBy);
            Assert.Equal(ActorId, user.UpdatedBy);
        }

        [Fact]
        public void Create_RejectsEmptyId()
        {
            Assert.Throws<ArgumentException>(() =>
                TenantUser.Create(
                    Guid.Empty, TenantUserKind.Owner, "cafe-owner", "cafe-owner", "Cafe Owner",
                    "owner@example.com", "owner@example.com", null, "hash",
                    "security-stamp", "concurrency-stamp", new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero)));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("ab")]
        [InlineData("Abc")]
        [InlineData("-abc")]
        [InlineData("abc-")]
        [InlineData("admin")]
        [InlineData("t")]
        public void Create_RejectsInvalidUserName(string? userName)
        {
            Assert.Throws<ArgumentException>(() =>
                TenantUser.Create(
                    UserId, TenantUserKind.Owner, userName!, "cafe-owner", "Cafe Owner",
                    "owner@example.com", "owner@example.com", null, "hash",
                    "security-stamp", "concurrency-stamp", new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero)));
        }

        [Fact]
        public void Create_AcceptsMinimumLengthUserName()
        {
            var user = TenantUser.Create(
                UserId, TenantUserKind.Owner, "abc", "abc", "Cafe Owner",
                null, null, null, "hash",
                "security-stamp", "concurrency-stamp", new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));

            Assert.Equal("abc", user.UserName);
        }

        [Fact]
        public void Create_RejectsOverlyLongFullName()
        {
            var longName = new string('a', 257);

            Assert.Throws<ArgumentException>(() =>
                TenantUser.Create(
                    UserId, TenantUserKind.Owner, "cafe-owner", "cafe-owner", longName,
                    "owner@example.com", "owner@example.com", null, "hash",
                    "security-stamp", "concurrency-stamp", new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero)));
        }

        [Fact]
        public void Create_AllowsNullEmail()
        {
            var user = TenantUser.Create(
                UserId, TenantUserKind.Owner, "cafe-owner", "cafe-owner", "Cafe Owner",
                null, null, null, "hash",
                "security-stamp", "concurrency-stamp", new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));

            Assert.Null(user.Email);
            Assert.Null(user.NormalizedEmail);
        }

        [Fact]
        public void Create_RejectsOverlyLongPasswordHash()
        {
            var longHash = new string('a', 513);

            Assert.Throws<ArgumentException>(() =>
                TenantUser.Create(
                    UserId, TenantUserKind.Owner, "cafe-owner", "cafe-owner", "Cafe Owner",
                    "owner@example.com", "owner@example.com", null, longHash,
                    "security-stamp", "concurrency-stamp", new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero)));
        }

        [Fact]
        public void Create_RejectsEmptySecurityStamp()
        {
            Assert.Throws<ArgumentException>(() =>
                TenantUser.Create(
                    UserId, TenantUserKind.Owner, "cafe-owner", "cafe-owner", "Cafe Owner",
                    "owner@example.com", "owner@example.com", null, "hash",
                    "", "concurrency-stamp", new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero)));
        }

        [Fact]
        public void Create_RejectsOverlyLongConcurrencyStamp()
        {
            var longStamp = new string('a', 65);

            Assert.Throws<ArgumentException>(() =>
                TenantUser.Create(
                    UserId, TenantUserKind.Owner, "cafe-owner", "cafe-owner", "Cafe Owner",
                    "owner@example.com", "owner@example.com", null, "hash",
                    "security-stamp", longStamp, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero)));
        }
    }
}
