using System;
using System.Linq;
using System.Reflection;
using CafeHub.Domain.Identity;
using CafeHub.Infrastructure.Persistence.Master.Identity;
using Xunit;

namespace CafeHub.UnitTests
{
    public sealed class PlatformAdminIdentityEntitiesTests
    {
        [Fact]
        public void EmailNormalization_TrimsAndLowerCases()
        {
            const string input = "  Admin@CafeHub.COM  ";
            var normalized = PlatformAdminEmail.Normalize(input);

            Assert.Equal("admin@cafehub.com", normalized);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t\r\n")]
        [InlineData(null)]
        public void EmailNormalization_RejectsEmptyOrWhitespace(string? input)
        {
            var exception = Assert.Throws<ArgumentException>(() => PlatformAdminEmail.Normalize(input!));
            Assert.Equal("email", exception.ParamName);
        }

        [Fact]
        public void EmailNormalization_IsIdempotent()
        {
            const string input = "  SuperAdmin@CafeHub.App ";
            var normalizedOnce = PlatformAdminEmail.Normalize(input);
            var normalizedTwice = PlatformAdminEmail.Normalize(normalizedOnce);

            Assert.Equal(normalizedOnce, normalizedTwice);
            Assert.Equal("superadmin@cafehub.app", normalizedTwice);
        }

        [Fact]
        public void RoleConstants_MatchExactlyTheErdSet()
        {
            var fields = typeof(PlatformAdminRoles)
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(f => f.IsLiteral && !f.IsInitOnly)
                .ToList();

            Assert.Single(fields);
            var singleField = fields[0];
            Assert.Equal(nameof(PlatformAdminRoles.SuperAdmin), singleField.Name);
            Assert.Equal("SuperAdmin", PlatformAdminRoles.SuperAdmin);
        }

        [Fact]
        public void PlatformAdminUser_UsesGuidKey_AndExposesErdExtraColumnsWithDefaults()
        {
            var user = new PlatformAdminUser();

            Assert.IsType<Guid>(user.Id);
            Assert.True(user.LockoutEnabled);
            Assert.False(user.EmailConfirmed);
            Assert.False(user.PhoneNumberConfirmed);
            Assert.False(user.TwoFactorEnabled);
            Assert.Equal(0, user.AccessFailedCount);

            Assert.True(user.IsActive);
            Assert.Null(user.DeletedAt);
            Assert.Null(user.DeletedBy);
            Assert.Null(user.CreatedBy);
            Assert.Null(user.UpdatedBy);
            Assert.Equal(default, user.CreatedAt);
            Assert.Equal(default, user.UpdatedAt);

            user.IsActive = false;
            Assert.False(user.IsActive);
            Assert.NotNull(user.DeletedAt);

            user.IsActive = true;
            Assert.True(user.IsActive);
            Assert.Null(user.DeletedAt);
        }

        [Fact]
        public void PlatformAdminRole_UsesGuidKey_AndExposesErdExtraColumns()
        {
            var role = new PlatformAdminRole("SuperAdmin");

            Assert.IsType<Guid>(role.Id);
            Assert.Equal("SuperAdmin", role.Name);
            Assert.False(string.IsNullOrWhiteSpace(role.ConcurrencyStamp));
            Assert.Null(role.CreatedBy);
            Assert.Null(role.UpdatedBy);
            Assert.Equal(default, role.CreatedAt);
            Assert.Equal(default, role.UpdatedAt);
        }

        [Fact]
        public void PlatformAdminUserRole_UsesGuidKeys_AndExposesCreatedAt()
        {
            var userRole = new PlatformAdminUserRole();

            Assert.IsType<Guid>(userRole.Id);
            Assert.IsType<Guid>(userRole.UserId);
            Assert.IsType<Guid>(userRole.RoleId);
            Assert.Equal(default, userRole.CreatedAt);
        }

        [Fact]
        public void PlatformAdminUserClaim_UsesGuidKeys_AndExposesCreatedAt()
        {
            var userClaim = new PlatformAdminUserClaim();

            Assert.IsType<Guid>(userClaim.Id);
            Assert.IsType<Guid>(userClaim.UserId);
            Assert.Equal(default, userClaim.CreatedAt);
        }

        [Fact]
        public void PlatformAdminRoleClaim_UsesGuidKeys_AndExposesCreatedAt()
        {
            var roleClaim = new PlatformAdminRoleClaim();

            Assert.IsType<Guid>(roleClaim.Id);
            Assert.IsType<Guid>(roleClaim.RoleId);
            Assert.Equal(default, roleClaim.CreatedAt);
        }

        [Fact]
        public void DomainAssembly_DoesNotReferenceIdentityOrAspNetCore()
        {
            var domainAssembly = typeof(PlatformAdminEmail).Assembly;
            var referencedAssemblyNames = domainAssembly.GetReferencedAssemblies()
                .Select(a => a.Name ?? string.Empty)
                .ToList();

            Assert.DoesNotContain(referencedAssemblyNames, name => name.StartsWith("Microsoft.AspNetCore", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(referencedAssemblyNames, name => name.StartsWith("Microsoft.Extensions.Identity", StringComparison.OrdinalIgnoreCase));
        }
    }
}

