using System.Linq;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace CafeHub.UnitTests.Tenants.Persistence
{
    public sealed class TenantModelConfigurationTests
    {
        private static IModel CreateModel()
        {
            var options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseNpgsql("Host=localhost;Database=test")
                .Options;

            using var context = new TenantDbContext(options);
            return context.GetService<IDesignTimeModel>().Model;
        }

        private static string[] ErdTables =>
        [
            "tenant_users",
            "tenant_roles",
            "tenant_user_roles",
            "tenant_user_claims",
            "tenant_role_claims",
            "branches",
            "cafe_settings",
            "tenant_audit_records",
        ];

        [Fact]
        public void Model_ContainsExactlyTheErdTables_AndNoMasterTables()
        {
            var model = CreateModel();
            var tableNames = model.GetEntityTypes()
                .Select(t => t.GetTableName()!)
                .OrderBy(t => t)
                .ToList();

            var expected = ErdTables.OrderBy(t => t).ToList();

            Assert.Equal(expected, tableNames);
            Assert.DoesNotContain(tableNames, t => t.StartsWith("platform_"));
            Assert.DoesNotContain(tableNames, t => t.StartsWith("onboarding_"));
            Assert.DoesNotContain(tableNames, t => t.StartsWith("subscription_"));
            Assert.DoesNotContain(tableNames, t => t.StartsWith("plan_"));
            Assert.DoesNotContain(tableNames, t => t.StartsWith("provisioning_"));
            Assert.DoesNotContain(tableNames, t => t.StartsWith("refresh_"));
        }

        [Fact]
        public void TenantUsersTable_MatchesErd()
        {
            var model = CreateModel();
            var entity = model.FindEntityType(typeof(TenantUser));
            Assert.NotNull(entity);

            Assert.Equal("tenant_users", entity.GetTableName());

            var pk = entity.FindPrimaryKey();
            Assert.NotNull(pk);
            Assert.Equal("pk_tenant_users", pk.GetName());
            var pkProp = Assert.Single(pk.Properties);
            Assert.Equal("id", pkProp.GetColumnName());
            Assert.Equal(typeof(Guid), pkProp.ClrType);
            Assert.False(pkProp.IsNullable);

            var userKind = entity.FindProperty(nameof(TenantUser.UserKind));
            Assert.NotNull(userKind);
            Assert.Equal("user_kind", userKind.GetColumnName());
            Assert.False(userKind.IsNullable);

            var userName = entity.FindProperty(nameof(TenantUser.UserName));
            Assert.NotNull(userName);
            Assert.Equal("user_name", userName.GetColumnName());
            Assert.False(userName.IsNullable);

            var normalizedUserName = entity.FindProperty(nameof(TenantUser.NormalizedUserName));
            Assert.NotNull(normalizedUserName);
            Assert.Equal("normalized_user_name", normalizedUserName.GetColumnName());
            Assert.False(normalizedUserName.IsNullable);

            var normalizedUserNameIndex = entity.GetIndexes().FirstOrDefault(i =>
                i.Properties.Count == 1 && i.Properties.Any(p => p.Name == nameof(TenantUser.NormalizedUserName)));
            Assert.NotNull(normalizedUserNameIndex);
            Assert.True(normalizedUserNameIndex.IsUnique);
            Assert.Equal("uq_tenant_users_normalized_user_name", normalizedUserNameIndex.GetDatabaseName());

            var fullName = entity.FindProperty(nameof(TenantUser.FullName));
            Assert.NotNull(fullName);
            Assert.Equal("full_name", fullName.GetColumnName());
            Assert.False(fullName.IsNullable);

            var email = entity.FindProperty(nameof(TenantUser.Email));
            Assert.NotNull(email);
            Assert.Equal("email", email.GetColumnName());
            Assert.True(email.IsNullable);

            var normalizedEmail = entity.FindProperty(nameof(TenantUser.NormalizedEmail));
            Assert.NotNull(normalizedEmail);
            Assert.Equal("normalized_email", normalizedEmail.GetColumnName());
            Assert.True(normalizedEmail.IsNullable);

            var normalizedEmailIndex = entity.GetIndexes().FirstOrDefault(i =>
                i.Properties.Count == 1 && i.Properties.Any(p => p.Name == nameof(TenantUser.NormalizedEmail)));
            Assert.NotNull(normalizedEmailIndex);
            Assert.False(normalizedEmailIndex.IsUnique);
            Assert.Equal("ix_tenant_users_normalized_email", normalizedEmailIndex.GetDatabaseName());

            var passwordHash = entity.FindProperty(nameof(TenantUser.PasswordHash));
            Assert.NotNull(passwordHash);
            Assert.Equal("password_hash", passwordHash.GetColumnName());
            Assert.False(passwordHash.IsNullable);

            var concurrencyStamp = entity.FindProperty(nameof(TenantUser.ConcurrencyStamp));
            Assert.NotNull(concurrencyStamp);
            Assert.Equal("concurrency_stamp", concurrencyStamp.GetColumnName());
            Assert.True(concurrencyStamp.IsConcurrencyToken);
            Assert.False(concurrencyStamp.IsNullable);

            var securityStamp = entity.FindProperty(nameof(TenantUser.SecurityStamp));
            Assert.NotNull(securityStamp);
            Assert.Equal("security_stamp", securityStamp.GetColumnName());
            Assert.False(securityStamp.IsNullable);

            var deletedAt = entity.FindProperty(nameof(TenantUser.DeletedAt));
            Assert.NotNull(deletedAt);
            Assert.Equal("deleted_at", deletedAt.GetColumnName());
            Assert.True(deletedAt.IsNullable);

            var createdAt = entity.FindProperty(nameof(TenantUser.CreatedAt));
            Assert.NotNull(createdAt);
            Assert.Equal("created_at", createdAt.GetColumnName());
            Assert.False(createdAt.IsNullable);

            var updatedAt = entity.FindProperty(nameof(TenantUser.UpdatedAt));
            Assert.NotNull(updatedAt);
            Assert.Equal("updated_at", updatedAt.GetColumnName());
            Assert.False(updatedAt.IsNullable);

            var checks = entity.GetCheckConstraints().ToList();
            Assert.Contains(checks, c => c.Name == "ck_tenant_users_kind");
            Assert.Contains(checks, c => c.Name == "ck_tenant_users_user_name_length");
            Assert.Contains(checks, c => c.Name == "ck_tenant_users_normalized_user_name_length");
            Assert.Contains(checks, c => c.Name == "ck_tenant_users_full_name_length");
            Assert.Contains(checks, c => c.Name == "ck_tenant_users_email_length");
            Assert.Contains(checks, c => c.Name == "ck_tenant_users_normalized_email_length");
            Assert.Contains(checks, c => c.Name == "ck_tenant_users_password_hash_length");
            Assert.Contains(checks, c => c.Name == "ck_tenant_users_phone_number_length");
            Assert.Contains(checks, c => c.Name == "ck_tenant_users_security_stamp_length");
            Assert.Contains(checks, c => c.Name == "ck_tenant_users_concurrency_stamp_length");

            var deletedByFk = entity.GetForeignKeys().SingleOrDefault(fk => fk.GetConstraintName() == "fk_tenant_users_deleted_by");
            Assert.NotNull(deletedByFk);
            Assert.Equal(typeof(TenantUser), deletedByFk.PrincipalEntityType.ClrType);

            var createdByFk = entity.GetForeignKeys().SingleOrDefault(fk => fk.GetConstraintName() == "fk_tenant_users_created_by");
            Assert.NotNull(createdByFk);
            Assert.Equal(typeof(TenantUser), createdByFk.PrincipalEntityType.ClrType);

            var updatedByFk = entity.GetForeignKeys().SingleOrDefault(fk => fk.GetConstraintName() == "fk_tenant_users_updated_by");
            Assert.NotNull(updatedByFk);
            Assert.Equal(typeof(TenantUser), updatedByFk.PrincipalEntityType.ClrType);
        }

        [Fact]
        public void TenantRolesTable_MatchesErd()
        {
            var model = CreateModel();
            var entity = model.FindEntityType(typeof(TenantRole));
            Assert.NotNull(entity);

            Assert.Equal("tenant_roles", entity.GetTableName());

            var pk = entity.FindPrimaryKey();
            Assert.NotNull(pk);
            Assert.Equal("pk_tenant_roles", pk.GetName());
            var pkProp = Assert.Single(pk.Properties);
            Assert.Equal("id", pkProp.GetColumnName());
            Assert.Equal(typeof(Guid), pkProp.ClrType);

            var name = entity.FindProperty(nameof(TenantRole.Name));
            Assert.NotNull(name);
            Assert.Equal("name", name.GetColumnName());
            Assert.False(name.IsNullable);

            var normalizedName = entity.FindProperty(nameof(TenantRole.NormalizedName));
            Assert.NotNull(normalizedName);
            Assert.Equal("normalized_name", normalizedName.GetColumnName());
            Assert.False(normalizedName.IsNullable);

            var normalizedNameIndex = entity.GetIndexes().FirstOrDefault(i =>
                i.Properties.Count == 1 && i.Properties.Any(p => p.Name == nameof(TenantRole.NormalizedName)));
            Assert.NotNull(normalizedNameIndex);
            Assert.True(normalizedNameIndex.IsUnique);
            Assert.Equal("uq_tenant_roles_normalized_name", normalizedNameIndex.GetDatabaseName());

            var concurrencyStamp = entity.FindProperty(nameof(TenantRole.ConcurrencyStamp));
            Assert.NotNull(concurrencyStamp);
            Assert.Equal("concurrency_stamp", concurrencyStamp.GetColumnName());
            Assert.True(concurrencyStamp.IsConcurrencyToken);
            Assert.False(concurrencyStamp.IsNullable);

            var checks = entity.GetCheckConstraints().ToList();
            Assert.Contains(checks, c => c.Name == "ck_tenant_roles_name_length");
            Assert.Contains(checks, c => c.Name == "ck_tenant_roles_normalized_name_length");
            Assert.Contains(checks, c => c.Name == "ck_tenant_roles_concurrency_stamp_length");

            var createdByFk = entity.GetForeignKeys().SingleOrDefault(fk => fk.GetConstraintName() == "fk_tenant_roles_created_by");
            Assert.NotNull(createdByFk);
            Assert.Equal(typeof(TenantUser), createdByFk.PrincipalEntityType.ClrType);
        }

        [Fact]
        public void TenantUserRolesTable_MatchesErd()
        {
            var model = CreateModel();
            var entity = model.FindEntityType(typeof(TenantUserRole));
            Assert.NotNull(entity);

            Assert.Equal("tenant_user_roles", entity.GetTableName());

            var pk = entity.FindPrimaryKey();
            Assert.NotNull(pk);
            Assert.Equal("pk_tenant_user_roles", pk.GetName());

            var userId = entity.FindProperty(nameof(TenantUserRole.TenantUserId));
            Assert.NotNull(userId);
            Assert.Equal("tenant_user_id", userId.GetColumnName());
            Assert.False(userId.IsNullable);

            var roleId = entity.FindProperty(nameof(TenantUserRole.RoleId));
            Assert.NotNull(roleId);
            Assert.Equal("role_id", roleId.GetColumnName());
            Assert.False(roleId.IsNullable);

            var index = entity.GetIndexes().FirstOrDefault(i =>
                i.Properties.Count == 2 && i.Properties.Any(p => p.Name == nameof(TenantUserRole.TenantUserId)) && i.Properties.Any(p => p.Name == nameof(TenantUserRole.RoleId)));
            Assert.NotNull(index);
            Assert.True(index.IsUnique);
            Assert.Equal("uq_tenant_user_roles_user_role", index.GetDatabaseName());
        }

        [Fact]
        public void TenantUserClaimsTable_MatchesErd()
        {
            var model = CreateModel();
            var entity = model.FindEntityType(typeof(TenantUserClaim));
            Assert.NotNull(entity);

            Assert.Equal("tenant_user_claims", entity.GetTableName());

            var pk = entity.FindPrimaryKey();
            Assert.NotNull(pk);
            Assert.Equal("pk_tenant_user_claims", pk.GetName());

            var userId = entity.FindProperty(nameof(TenantUserClaim.TenantUserId));
            Assert.NotNull(userId);
            Assert.Equal("tenant_user_id", userId.GetColumnName());
            Assert.False(userId.IsNullable);

            var claimType = entity.FindProperty(nameof(TenantUserClaim.ClaimType));
            Assert.NotNull(claimType);
            Assert.Equal("claim_type", claimType.GetColumnName());
            Assert.False(claimType.IsNullable);

            var claimValue = entity.FindProperty(nameof(TenantUserClaim.ClaimValue));
            Assert.NotNull(claimValue);
            Assert.Equal("claim_value", claimValue.GetColumnName());
            Assert.True(claimValue.IsNullable);
        }

        [Fact]
        public void TenantRoleClaimsTable_MatchesErd()
        {
            var model = CreateModel();
            var entity = model.FindEntityType(typeof(TenantRoleClaim));
            Assert.NotNull(entity);

            Assert.Equal("tenant_role_claims", entity.GetTableName());

            var pk = entity.FindPrimaryKey();
            Assert.NotNull(pk);
            Assert.Equal("pk_tenant_role_claims", pk.GetName());

            var roleId = entity.FindProperty(nameof(TenantRoleClaim.RoleId));
            Assert.NotNull(roleId);
            Assert.Equal("role_id", roleId.GetColumnName());
            Assert.False(roleId.IsNullable);

            var claimType = entity.FindProperty(nameof(TenantRoleClaim.ClaimType));
            Assert.NotNull(claimType);
            Assert.Equal("claim_type", claimType.GetColumnName());
            Assert.False(claimType.IsNullable);

            var claimValue = entity.FindProperty(nameof(TenantRoleClaim.ClaimValue));
            Assert.NotNull(claimValue);
            Assert.Equal("claim_value", claimValue.GetColumnName());
            Assert.True(claimValue.IsNullable);
        }

        [Fact]
        public void BranchesTable_MatchesErd()
        {
            var model = CreateModel();
            var entity = model.FindEntityType(typeof(Branch));
            Assert.NotNull(entity);

            Assert.Equal("branches", entity.GetTableName());

            var pk = entity.FindPrimaryKey();
            Assert.NotNull(pk);
            Assert.Equal("pk_branches", pk.GetName());

            var name = entity.FindProperty(nameof(Branch.Name));
            Assert.NotNull(name);
            Assert.Equal("name", name.GetColumnName());
            Assert.False(name.IsNullable);

            var lifecycleStatus = entity.FindProperty(nameof(Branch.LifecycleStatus));
            Assert.NotNull(lifecycleStatus);
            Assert.Equal("lifecycle_status", lifecycleStatus.GetColumnName());
            Assert.False(lifecycleStatus.IsNullable);

            var checks = entity.GetCheckConstraints().ToList();
            Assert.Contains(checks, c => c.Name == "ck_branches_lifecycle_status");
            Assert.Contains(checks, c => c.Name == "ck_branches_name_length");
            Assert.Contains(checks, c => c.Name == "ck_branches_address_length");
            Assert.Contains(checks, c => c.Name == "ck_branches_contact_phone_length");
            Assert.Contains(checks, c => c.Name == "ck_branches_opening_hours_summary_length");
        }

        [Fact]
        public void CafeSettingsTable_MatchesErd()
        {
            var model = CreateModel();
            var entity = model.FindEntityType(typeof(CafeSettings));
            Assert.NotNull(entity);

            Assert.Equal("cafe_settings", entity.GetTableName());

            var pk = entity.FindPrimaryKey();
            Assert.NotNull(pk);
            Assert.Equal("pk_cafe_settings", pk.GetName());

            var cafeName = entity.FindProperty(nameof(CafeSettings.CafeName));
            Assert.NotNull(cafeName);
            Assert.Equal("cafe_name", cafeName.GetColumnName());
            Assert.False(cafeName.IsNullable);

            var timezone = entity.FindProperty(nameof(CafeSettings.Timezone));
            Assert.NotNull(timezone);
            Assert.Equal("timezone", timezone.GetColumnName());
            Assert.False(timezone.IsNullable);

            var logoMediaReference = entity.FindProperty(nameof(CafeSettings.LogoMediaReference));
            Assert.NotNull(logoMediaReference);
            Assert.Equal("logo_media_reference", logoMediaReference.GetColumnName());
            Assert.True(logoMediaReference.IsNullable);

            var primaryColor = entity.FindProperty(nameof(CafeSettings.PrimaryColor));
            Assert.NotNull(primaryColor);
            Assert.Equal("primary_color", primaryColor.GetColumnName());
            Assert.True(primaryColor.IsNullable);

            var checks = entity.GetCheckConstraints().ToList();
            Assert.Contains(checks, c => c.Name == "ck_cafe_settings_cafe_name_length");
            Assert.Contains(checks, c => c.Name == "ck_cafe_settings_color_format");
            Assert.Contains(checks, c => c.Name == "ck_cafe_settings_timezone");
        }

        [Fact]
        public void TenantAuditRecordsTable_MatchesErd()
        {
            var model = CreateModel();
            var entity = model.FindEntityType(typeof(TenantAuditRecord));
            Assert.NotNull(entity);

            Assert.Equal("tenant_audit_records", entity.GetTableName());

            var pk = entity.FindPrimaryKey();
            Assert.NotNull(pk);
            Assert.Equal("pk_tenant_audit_records", pk.GetName());

            var recordedAt = entity.FindProperty(nameof(TenantAuditRecord.RecordedAt));
            Assert.NotNull(recordedAt);
            Assert.Equal("recorded_at", recordedAt.GetColumnName());
            Assert.False(recordedAt.IsNullable);

            var actorKind = entity.FindProperty(nameof(TenantAuditRecord.ActorKind));
            Assert.NotNull(actorKind);
            Assert.Equal("actor_kind", actorKind.GetColumnName());
            Assert.False(actorKind.IsNullable);

            var actionCode = entity.FindProperty(nameof(TenantAuditRecord.ActionCode));
            Assert.NotNull(actionCode);
            Assert.Equal("action_code", actionCode.GetColumnName());
            Assert.False(actionCode.IsNullable);

            var subjectType = entity.FindProperty(nameof(TenantAuditRecord.SubjectType));
            Assert.NotNull(subjectType);
            Assert.Equal("subject_type", subjectType.GetColumnName());
            Assert.False(subjectType.IsNullable);

            var subjectId = entity.FindProperty(nameof(TenantAuditRecord.SubjectId));
            Assert.NotNull(subjectId);
            Assert.Equal("subject_id", subjectId.GetColumnName());
            Assert.True(subjectId.IsNullable);

            var beforeState = entity.FindProperty(nameof(TenantAuditRecord.BeforeState));
            Assert.NotNull(beforeState);
            Assert.Equal("before_state", beforeState.GetColumnName());
            Assert.Equal("jsonb", beforeState.GetColumnType());

            var afterState = entity.FindProperty(nameof(TenantAuditRecord.AfterState));
            Assert.NotNull(afterState);
            Assert.Equal("after_state", afterState.GetColumnName());
            Assert.Equal("jsonb", afterState.GetColumnType());

            var checks = entity.GetCheckConstraints().ToList();
            Assert.Contains(checks, c => c.Name == "ck_tenant_audit_records_kind");
            Assert.Contains(checks, c => c.Name == "ck_tenant_audit_records_action_code_length");
            Assert.Contains(checks, c => c.Name == "ck_tenant_audit_records_subject_type_length");

            var actorFk = entity.GetForeignKeys().SingleOrDefault(fk => fk.GetConstraintName() == "fk_tenant_audit_records_actor_user_id");
            Assert.NotNull(actorFk);
            Assert.Equal(typeof(TenantUser), actorFk.PrincipalEntityType.ClrType);
        }
    }
}
