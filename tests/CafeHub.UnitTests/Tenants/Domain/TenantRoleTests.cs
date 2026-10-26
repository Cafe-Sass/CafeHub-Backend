using CafeHub.Domain.Identity;
using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Tenants.Domain
{
    public sealed class TenantRoleTests
    {
        private static readonly Guid RoleId = Guid.CreateVersion7();
        private static readonly Guid ActorId = Guid.CreateVersion7();

        [Fact]
        public void Create_SetsAllProperties()
        {
            var role = TenantRole.Create(
                RoleId,
                "Owner",
                "owner",
                "concurrency-stamp-value",
                new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero),
                ActorId);

            Assert.Equal(RoleId, role.Id);
            Assert.Equal("Owner", role.Name);
            Assert.Equal("owner", role.NormalizedName);
            Assert.Equal("concurrency-stamp-value", role.ConcurrencyStamp);
            Assert.Equal(new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero), role.CreatedAt);
            Assert.Equal(new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero), role.UpdatedAt);
            Assert.Equal(ActorId, role.CreatedBy);
            Assert.Equal(ActorId, role.UpdatedBy);
        }

        [Fact]
        public void Create_RejectsEmptyId()
        {
            Assert.Throws<ArgumentException>(() =>
                TenantRole.Create(
                    Guid.Empty, "Owner", "owner", "stamp", new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero)));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Create_RejectsInvalidRoleName(string? name)
        {
            Assert.Throws<ArgumentException>(() =>
                TenantRole.Create(
                    RoleId, name!, "owner", "stamp", new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero)));
        }

        [Fact]
        public void Create_AcceptsValidRoleName()
        {
            var role = TenantRole.Create(
                RoleId, "cafe_manager", "cafe_manager", "stamp", new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));

            Assert.Equal("cafe_manager", role.Name);
        }

        [Fact]
        public void Create_RejectsEmptyNormalizedName()
        {
            Assert.Throws<ArgumentException>(() =>
                TenantRole.Create(
                    RoleId, "Owner", "", "stamp", new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero)));
        }

        [Fact]
        public void Create_RejectsEmptyConcurrencyStamp()
        {
            Assert.Throws<ArgumentException>(() =>
                TenantRole.Create(
                    RoleId, "Owner", "owner", "", new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero)));
        }
    }

    public sealed class TenantUserRoleTests
    {
        private static readonly Guid Id = Guid.CreateVersion7();
        private static readonly Guid UserId = Guid.CreateVersion7();
        private static readonly Guid RoleId = Guid.CreateVersion7();

        [Fact]
        public void Create_SetsAllProperties()
        {
            var membership = TenantUserRole.Create(Id, UserId, RoleId, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));

            Assert.Equal(Id, membership.Id);
            Assert.Equal(UserId, membership.TenantUserId);
            Assert.Equal(RoleId, membership.RoleId);
            Assert.Equal(new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero), membership.CreatedAt);
        }

        [Fact]
        public void Create_RejectsEmptyId()
        {
            Assert.Throws<ArgumentException>(() =>
                TenantUserRole.Create(Guid.Empty, UserId, RoleId, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero)));
        }

        [Fact]
        public void Create_RejectsEmptyUserId()
        {
            Assert.Throws<ArgumentException>(() =>
                TenantUserRole.Create(Id, Guid.Empty, RoleId, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero)));
        }

        [Fact]
        public void Create_RejectsEmptyRoleId()
        {
            Assert.Throws<ArgumentException>(() =>
                TenantUserRole.Create(Id, UserId, Guid.Empty, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero)));
        }
    }

    public sealed class TenantUserClaimTests
    {
        private static readonly Guid Id = Guid.CreateVersion7();
        private static readonly Guid UserId = Guid.CreateVersion7();

        [Fact]
        public void Create_SetsAllProperties()
        {
            var claim = TenantUserClaim.Create(Id, UserId, "cafes.read", "true", new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));

            Assert.Equal(Id, claim.Id);
            Assert.Equal(UserId, claim.TenantUserId);
            Assert.Equal("cafes.read", claim.ClaimType);
            Assert.Equal("true", claim.ClaimValue);
            Assert.Equal(new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero), claim.CreatedAt);
        }

        [Fact]
        public void Create_AllowsNullClaimValue()
        {
            var claim = TenantUserClaim.Create(Id, UserId, "cafes.read", null, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));

            Assert.Null(claim.ClaimValue);
        }

        [Fact]
        public void Create_RejectsOverlyLongClaimValue()
        {
            var longValue = new string('a', 1025);

            Assert.Throws<ArgumentException>(() =>
                TenantUserClaim.Create(Id, UserId, "cafes.read", longValue, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero)));
        }
    }

    public sealed class TenantRoleClaimTests
    {
        private static readonly Guid Id = Guid.CreateVersion7();
        private static readonly Guid RoleId = Guid.CreateVersion7();

        [Fact]
        public void Create_SetsAllProperties()
        {
            var claim = TenantRoleClaim.Create(Id, RoleId, "cafes.manage", "true", new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));

            Assert.Equal(Id, claim.Id);
            Assert.Equal(RoleId, claim.RoleId);
            Assert.Equal("cafes.manage", claim.ClaimType);
            Assert.Equal("true", claim.ClaimValue);
        }

        [Fact]
        public void Create_RejectsEmptyRoleId()
        {
            Assert.Throws<ArgumentException>(() =>
                TenantRoleClaim.Create(Id, Guid.Empty, "cafes.manage", null, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero)));
        }
    }
}
