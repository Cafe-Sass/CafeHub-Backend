using System;
using System.Linq;
using CafeHub.Infrastructure.Persistence.Master;
using CafeHub.Infrastructure.Persistence.Master.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace CafeHub.UnitTests
{
    public sealed class MasterIdentityEfConfigurationTests
    {
        private static IModel CreateModel()
        {
            var options = new DbContextOptionsBuilder<MasterDbContext>()
                .UseNpgsql("Host=localhost;Database=test")
                .Options;

            using var context = new MasterDbContext(options);
            return context.GetService<IDesignTimeModel>().Model;
        }

        [Fact]
        public void Model_ContainsExactlyTheFiveIdentityTables_AndNoExtraTables()
        {
            var model = CreateModel();
            var entityTypes = model.GetEntityTypes().ToList();

            var tableNames = entityTypes
                .Select(t => t.GetTableName()!)
                .OrderBy(t => t)
                .ToList();

            var expectedTables = new[]
            {
                "platform_admin_role_claims",
                "platform_admin_roles",
                "platform_admin_user_claims",
                "platform_admin_user_roles",
                "platform_admins",
            }.OrderBy(t => t).ToList();

            Assert.Equal(expectedTables, tableNames);
            Assert.DoesNotContain(tableNames, t => t != null && (t.Contains("login") || t.Contains("token") || t.Contains("tenant")));
        }

        [Fact]
        public void PlatformAdminsTable_MatchesErd()
        {
            var model = CreateModel();
            var entity = model.FindEntityType(typeof(PlatformAdminUser));
            Assert.NotNull(entity);

            Assert.Equal("platform_admins", entity.GetTableName());

            var pk = entity.FindPrimaryKey();
            Assert.NotNull(pk);
            Assert.Equal("pk_platform_admins", pk.GetName());
            var pkProp = Assert.Single(pk.Properties);
            Assert.Equal("id", pkProp.GetColumnName());
            Assert.Equal(typeof(Guid), pkProp.ClrType);

            var userName = entity.FindProperty(nameof(PlatformAdminUser.UserName));
            Assert.NotNull(userName);
            Assert.Equal("user_name", userName.GetColumnName());
            Assert.False(userName.IsNullable);

            var normalizedUserName = entity.FindProperty(nameof(PlatformAdminUser.NormalizedUserName));
            Assert.NotNull(normalizedUserName);
            Assert.Equal("normalized_user_name", normalizedUserName.GetColumnName());
            Assert.False(normalizedUserName.IsNullable);

            var normalizedUserNameIndex = entity.GetIndexes().FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(PlatformAdminUser.NormalizedUserName)));
            Assert.NotNull(normalizedUserNameIndex);
            Assert.True(normalizedUserNameIndex.IsUnique);
            Assert.Equal("uq_platform_admins_normalized_user_name", normalizedUserNameIndex.GetDatabaseName());

            var email = entity.FindProperty(nameof(PlatformAdminUser.Email));
            Assert.NotNull(email);
            Assert.Equal("email", email.GetColumnName());
            Assert.True(email.IsNullable);

            var normalizedEmail = entity.FindProperty(nameof(PlatformAdminUser.NormalizedEmail));
            Assert.NotNull(normalizedEmail);
            Assert.Equal("normalized_email", normalizedEmail.GetColumnName());
            Assert.True(normalizedEmail.IsNullable);

            var normalizedEmailIndex = entity.GetIndexes().FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(PlatformAdminUser.NormalizedEmail)));
            Assert.NotNull(normalizedEmailIndex);
            Assert.True(normalizedEmailIndex.IsUnique);
            Assert.Equal("uq_platform_admins_normalized_email", normalizedEmailIndex.GetDatabaseName());
            Assert.Equal("normalized_email IS NOT NULL", normalizedEmailIndex.GetFilter());

            var concurrencyStamp = entity.FindProperty(nameof(PlatformAdminUser.ConcurrencyStamp));
            Assert.NotNull(concurrencyStamp);
            Assert.Equal("concurrency_stamp", concurrencyStamp.GetColumnName());
            Assert.True(concurrencyStamp.IsConcurrencyToken);
            Assert.False(concurrencyStamp.IsNullable);

            var securityStamp = entity.FindProperty(nameof(PlatformAdminUser.SecurityStamp));
            Assert.NotNull(securityStamp);
            Assert.Equal("security_stamp", securityStamp.GetColumnName());
            Assert.False(securityStamp.IsNullable);

            var lockoutEnd = entity.FindProperty(nameof(PlatformAdminUser.LockoutEnd));
            Assert.NotNull(lockoutEnd);
            Assert.Equal("lockout_end_at", lockoutEnd.GetColumnName());
            Assert.True(lockoutEnd.IsNullable);

            Assert.Null(entity.FindProperty("IsActive"));

            var checks = entity.GetCheckConstraints().ToList();
            Assert.Contains(checks, c => c.Name == "ck_platform_admins_user_name_length");
            Assert.Contains(checks, c => c.Name == "ck_platform_admins_normalized_user_name_length");
            Assert.Contains(checks, c => c.Name == "ck_platform_admins_email_length");
            Assert.Contains(checks, c => c.Name == "ck_platform_admins_normalized_email_length");
            Assert.Contains(checks, c => c.Name == "ck_platform_admins_password_hash_length");
            Assert.Contains(checks, c => c.Name == "ck_platform_admins_phone_number_length");
            Assert.Contains(checks, c => c.Name == "ck_platform_admins_security_stamp_length");
            Assert.Contains(checks, c => c.Name == "ck_platform_admins_concurrency_stamp_length");
        }

        [Fact]
        public void PlatformAdminRolesTable_MatchesErd()
        {
            var model = CreateModel();
            var entity = model.FindEntityType(typeof(PlatformAdminRole));
            Assert.NotNull(entity);

            Assert.Equal("platform_admin_roles", entity.GetTableName());

            var pk = entity.FindPrimaryKey();
            Assert.NotNull(pk);
            Assert.Equal("pk_platform_admin_roles", pk.GetName());
            var pkProp = Assert.Single(pk.Properties);
            Assert.Equal("id", pkProp.GetColumnName());
            Assert.Equal(typeof(Guid), pkProp.ClrType);

            var name = entity.FindProperty(nameof(PlatformAdminRole.Name));
            Assert.NotNull(name);
            Assert.Equal("name", name.GetColumnName());
            Assert.False(name.IsNullable);

            var normalizedName = entity.FindProperty(nameof(PlatformAdminRole.NormalizedName));
            Assert.NotNull(normalizedName);
            Assert.Equal("normalized_name", normalizedName.GetColumnName());
            Assert.False(normalizedName.IsNullable);

            var normalizedNameIndex = entity.GetIndexes().FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(PlatformAdminRole.NormalizedName)));
            Assert.NotNull(normalizedNameIndex);
            Assert.True(normalizedNameIndex.IsUnique);
            Assert.Equal("uq_platform_admin_roles_normalized_name", normalizedNameIndex.GetDatabaseName());

            var concurrencyStamp = entity.FindProperty(nameof(PlatformAdminRole.ConcurrencyStamp));
            Assert.NotNull(concurrencyStamp);
            Assert.Equal("concurrency_stamp", concurrencyStamp.GetColumnName());
            Assert.True(concurrencyStamp.IsConcurrencyToken);
            Assert.False(concurrencyStamp.IsNullable);

            var checks = entity.GetCheckConstraints().ToList();
            Assert.Contains(checks, c => c.Name == "ck_platform_admin_roles_name_length");
            Assert.Contains(checks, c => c.Name == "ck_platform_admin_roles_normalized_name_length");
            Assert.Contains(checks, c => c.Name == "ck_platform_admin_roles_concurrency_stamp_length");
        }

        [Fact]
        public void PlatformAdminUserRolesTable_MatchesErd()
        {
            var model = CreateModel();
            var entity = model.FindEntityType(typeof(PlatformAdminUserRole));
            Assert.NotNull(entity);

            Assert.Equal("platform_admin_user_roles", entity.GetTableName());

            var pk = entity.FindPrimaryKey();
            Assert.NotNull(pk);
            Assert.Equal("pk_platform_admin_user_roles", pk.GetName());
            var pkProp = Assert.Single(pk.Properties);
            Assert.Equal("id", pkProp.GetColumnName());
            Assert.Equal(typeof(Guid), pkProp.ClrType);

            var userId = entity.FindProperty(nameof(PlatformAdminUserRole.UserId));
            Assert.NotNull(userId);
            Assert.Equal("platform_admin_id", userId.GetColumnName());
            Assert.False(userId.IsNullable);

            var roleId = entity.FindProperty(nameof(PlatformAdminUserRole.RoleId));
            Assert.NotNull(roleId);
            Assert.Equal("role_id", roleId.GetColumnName());
            Assert.False(roleId.IsNullable);

            var userRoleIndex = entity.GetIndexes().FirstOrDefault(i => i.Properties.Count == 2 && i.Properties.Any(p => p.Name == nameof(PlatformAdminUserRole.UserId)) && i.Properties.Any(p => p.Name == nameof(PlatformAdminUserRole.RoleId)));
            Assert.NotNull(userRoleIndex);
            Assert.True(userRoleIndex.IsUnique);
            Assert.Equal("uq_platform_admin_user_roles_user_role", userRoleIndex.GetDatabaseName());
        }

        [Fact]
        public void PlatformAdminUserClaimsTable_MatchesErd()
        {
            var model = CreateModel();
            var entity = model.FindEntityType(typeof(PlatformAdminUserClaim));
            Assert.NotNull(entity);

            Assert.Equal("platform_admin_user_claims", entity.GetTableName());

            var pk = entity.FindPrimaryKey();
            Assert.NotNull(pk);
            Assert.Equal("pk_platform_admin_user_claims", pk.GetName());
            var pkProp = Assert.Single(pk.Properties);
            Assert.Equal("id", pkProp.GetColumnName());
            Assert.Equal(typeof(Guid), pkProp.ClrType);

            var userId = entity.FindProperty(nameof(PlatformAdminUserClaim.UserId));
            Assert.NotNull(userId);
            Assert.Equal("platform_admin_id", userId.GetColumnName());
            Assert.False(userId.IsNullable);

            var claimType = entity.FindProperty(nameof(PlatformAdminUserClaim.ClaimType));
            Assert.NotNull(claimType);
            Assert.Equal("claim_type", claimType.GetColumnName());
            Assert.False(claimType.IsNullable);

            var claimValue = entity.FindProperty(nameof(PlatformAdminUserClaim.ClaimValue));
            Assert.NotNull(claimValue);
            Assert.Equal("claim_value", claimValue.GetColumnName());
            Assert.True(claimValue.IsNullable);

            var checks = entity.GetCheckConstraints().ToList();
            Assert.Contains(checks, c => c.Name == "ck_platform_admin_user_claims_claim_type_length");
            Assert.Contains(checks, c => c.Name == "ck_platform_admin_user_claims_claim_value_length");
        }

        [Fact]
        public void PlatformAdminRoleClaimsTable_MatchesErd()
        {
            var model = CreateModel();
            var entity = model.FindEntityType(typeof(PlatformAdminRoleClaim));
            Assert.NotNull(entity);

            Assert.Equal("platform_admin_role_claims", entity.GetTableName());

            var pk = entity.FindPrimaryKey();
            Assert.NotNull(pk);
            Assert.Equal("pk_platform_admin_role_claims", pk.GetName());
            var pkProp = Assert.Single(pk.Properties);
            Assert.Equal("id", pkProp.GetColumnName());
            Assert.Equal(typeof(Guid), pkProp.ClrType);

            var roleId = entity.FindProperty(nameof(PlatformAdminRoleClaim.RoleId));
            Assert.NotNull(roleId);
            Assert.Equal("role_id", roleId.GetColumnName());
            Assert.False(roleId.IsNullable);

            var claimType = entity.FindProperty(nameof(PlatformAdminRoleClaim.ClaimType));
            Assert.NotNull(claimType);
            Assert.Equal("claim_type", claimType.GetColumnName());
            Assert.False(claimType.IsNullable);

            var claimValue = entity.FindProperty(nameof(PlatformAdminRoleClaim.ClaimValue));
            Assert.NotNull(claimValue);
            Assert.Equal("claim_value", claimValue.GetColumnName());
            Assert.True(claimValue.IsNullable);

            var checks = entity.GetCheckConstraints().ToList();
            Assert.Contains(checks, c => c.Name == "ck_platform_admin_role_claims_claim_type_length");
            Assert.Contains(checks, c => c.Name == "ck_platform_admin_role_claims_claim_value_length");
        }
    }
}

