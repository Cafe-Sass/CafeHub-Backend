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
            "branch_tables",
            "stations",
            "tenant_user_stations",
            "menu_categories",
            "products",
            "product_sizes",
            "product_addons",
            "product_branch_station_slots",
            "product_station_assignments",
            "product_routing_readiness",
            "product_tags",
            "branch_product_availability",
            "cafe_settings",
            "cafe_payment_settings",
            "tenant_audit_records",
            "refresh_tokens",
            "orders",
            "order_items",
            "order_item_addons",
            "order_number_counters",
            "order_response_misses",
            "order_response_miss_holders",
            "guest_mutes",
            "bills",
            "payments",
            "receipts",
            "receipt_lines",
            "receipt_payments",
            "receipt_number_counters",
            "wifi_settings",
            "branch_wifi_networks",
            "wifi_packages",
            "branch_wifi_qr",
            "wifi_vouchers",
            "printers",
            "print_routes",
            "print_jobs",
            "print_connectors",
            "inventory_items",
            "inventory_batches",
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
            Assert.True(normalizedEmailIndex.IsUnique);
            Assert.Equal("normalized_email IS NOT NULL", normalizedEmailIndex.GetFilter());
            Assert.Equal("uq_tenant_users_normalized_email", normalizedEmailIndex.GetDatabaseName());

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

            var xmin = entity.FindProperty("xmin");
            Assert.NotNull(xmin);
            Assert.Equal(typeof(uint), xmin.ClrType);
            Assert.True(xmin.IsConcurrencyToken);
            Assert.False(xmin.IsNullable);
        }

        [Fact]
        public void StationsTable_MatchesErd()
        {
            var model = CreateModel();
            var entity = model.FindEntityType(typeof(Station));
            Assert.NotNull(entity);

            Assert.Equal("stations", entity.GetTableName());

            var pk = entity.FindPrimaryKey();
            Assert.NotNull(pk);
            Assert.Equal("pk_stations", pk.GetName());
            Assert.Equal("id", Assert.Single(pk.Properties).GetColumnName());

            var branchId = entity.FindProperty(nameof(Station.BranchId));
            Assert.NotNull(branchId);
            Assert.Equal("branch_id", branchId.GetColumnName());
            Assert.False(branchId.IsNullable);

            var name = entity.FindProperty(nameof(Station.Name));
            Assert.NotNull(name);
            Assert.Equal("name", name.GetColumnName());
            Assert.False(name.IsNullable);

            var status = entity.FindProperty(nameof(Station.Status));
            Assert.NotNull(status);
            Assert.Equal("status", status.GetColumnName());
            Assert.False(status.IsNullable);

            var sortOrder = entity.FindProperty(nameof(Station.SortOrder));
            Assert.NotNull(sortOrder);
            Assert.Equal("sort_order", sortOrder.GetColumnName());
            Assert.False(sortOrder.IsNullable);

            var branchIndex = entity.GetIndexes().SingleOrDefault(index =>
                index.GetDatabaseName() == "ix_stations_branch_id");
            Assert.NotNull(branchIndex);
            Assert.Equal("branch_id", Assert.Single(branchIndex.Properties).GetColumnName());

            var checks = entity.GetCheckConstraints().ToList();
            Assert.Contains(checks, c => c.Name == "ck_stations_name_length");
            Assert.Contains(checks, c => c.Name == "ck_stations_status");
            Assert.Contains(checks, c => c.Name == "ck_stations_sort_order");

            var branchFk = entity.GetForeignKeys().SingleOrDefault(fk => fk.GetConstraintName() == "fk_stations_branch_id");
            Assert.NotNull(branchFk);
            Assert.Equal(typeof(Branch), branchFk.PrincipalEntityType.ClrType);
            Assert.Equal(DeleteBehavior.Restrict, branchFk.DeleteBehavior);

            var xmin = entity.FindProperty("xmin");
            Assert.NotNull(xmin);
            Assert.Equal(typeof(uint), xmin.ClrType);
            Assert.True(xmin.IsConcurrencyToken);
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

            var currencyCode = entity.FindProperty(nameof(CafeSettings.CurrencyCode));
            Assert.NotNull(currencyCode);
            Assert.Equal("currency_code", currencyCode.GetColumnName());
            Assert.False(currencyCode.IsNullable);
            Assert.Equal("text", currencyCode.GetColumnType());
            Assert.Equal(CafeSettings.DefaultCurrencyCode, currencyCode.GetDefaultValue());

            var checks = entity.GetCheckConstraints().ToList();
            Assert.Contains(checks, c => c.Name == "ck_cafe_settings_cafe_name_length");
            Assert.Contains(checks, c => c.Name == "ck_cafe_settings_color_format");
            Assert.Contains(checks, c => c.Name == "ck_cafe_settings_timezone");
            Assert.Contains(checks, c => c.Name == "ck_cafe_settings_currency_code_format");
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

        [Fact]
        public void BranchTablesTable_MatchesErd()
        {
            var model = CreateModel();
            var entity = model.FindEntityType(typeof(BranchTable));
            Assert.NotNull(entity);

            Assert.Equal("branch_tables", entity.GetTableName());

            var pk = entity.FindPrimaryKey();
            Assert.NotNull(pk);
            Assert.Equal("pk_branch_tables", pk.GetName());
            Assert.Equal("id", Assert.Single(pk.Properties).GetColumnName());

            var branchId = entity.FindProperty(nameof(BranchTable.BranchId));
            Assert.NotNull(branchId);
            Assert.Equal("branch_id", branchId.GetColumnName());
            Assert.False(branchId.IsNullable);

            var number = entity.FindProperty(nameof(BranchTable.Number));
            Assert.NotNull(number);
            Assert.Equal("number", number.GetColumnName());
            Assert.False(number.IsNullable);

            var label = entity.FindProperty(nameof(BranchTable.Label));
            Assert.NotNull(label);
            Assert.Equal("label", label.GetColumnName());
            Assert.True(label.IsNullable);

            var qrCode = entity.FindProperty(nameof(BranchTable.QrCode));
            Assert.NotNull(qrCode);
            Assert.Equal("qr_code", qrCode.GetColumnName());
            Assert.False(qrCode.IsNullable);

            var status = entity.FindProperty(nameof(BranchTable.Status));
            Assert.NotNull(status);
            Assert.Equal("status", status.GetColumnName());
            Assert.False(status.IsNullable);

            var createdAt = entity.FindProperty(nameof(BranchTable.CreatedAt));
            Assert.NotNull(createdAt);
            Assert.Equal("created_at", createdAt.GetColumnName());
            Assert.False(createdAt.IsNullable);

            var updatedAt = entity.FindProperty(nameof(BranchTable.UpdatedAt));
            Assert.NotNull(updatedAt);
            Assert.Equal("updated_at", updatedAt.GetColumnName());
            Assert.False(updatedAt.IsNullable);

            var createdBy = entity.FindProperty(nameof(BranchTable.CreatedBy));
            Assert.NotNull(createdBy);
            Assert.Equal("created_by", createdBy.GetColumnName());
            Assert.True(createdBy.IsNullable);

            var updatedBy = entity.FindProperty(nameof(BranchTable.UpdatedBy));
            Assert.NotNull(updatedBy);
            Assert.Equal("updated_by", updatedBy.GetColumnName());
            Assert.True(updatedBy.IsNullable);

            var branchIndex = entity.GetIndexes().SingleOrDefault(index =>
                index.GetDatabaseName() == "ix_branch_tables_branch_id");
            Assert.NotNull(branchIndex);
            Assert.Equal("branch_id", Assert.Single(branchIndex.Properties).GetColumnName());

            var qrCodeIndex = entity.GetIndexes().SingleOrDefault(index =>
                index.GetDatabaseName() == "uq_branch_tables_qr_code");
            Assert.NotNull(qrCodeIndex);
            Assert.True(qrCodeIndex.IsUnique);
            Assert.Equal("qr_code", Assert.Single(qrCodeIndex.Properties).GetColumnName());

            var checks = entity.GetCheckConstraints().ToList();
            Assert.Contains(checks, c => c.Name == "ck_branch_tables_number_range");
            Assert.Contains(checks, c => c.Name == "ck_branch_tables_label_length");
            Assert.Contains(checks, c => c.Name == "ck_branch_tables_qr_code_format");
            Assert.Contains(checks, c => c.Name == "ck_branch_tables_status");

            var branchFk = entity.GetForeignKeys().SingleOrDefault(fk => fk.GetConstraintName() == "fk_branch_tables_branch_id");
            Assert.NotNull(branchFk);
            Assert.Equal(typeof(Branch), branchFk.PrincipalEntityType.ClrType);
            Assert.Equal(DeleteBehavior.Restrict, branchFk.DeleteBehavior);

            var xmin = entity.FindProperty("xmin");
            Assert.NotNull(xmin);
            Assert.Equal(typeof(uint), xmin.ClrType);
            Assert.True(xmin.IsConcurrencyToken);
            Assert.False(xmin.IsNullable);
        }

        [Fact]
        public void ProductsTable_MatchesErd()
        {
            var model = CreateModel();
            var entity = model.FindEntityType(typeof(Product));
            Assert.NotNull(entity);

            Assert.Equal("products", entity.GetTableName());

            var pk = entity.FindPrimaryKey();
            Assert.NotNull(pk);
            Assert.Equal("pk_products", pk.GetName());
            Assert.Equal("id", Assert.Single(pk.Properties).GetColumnName());

            var categoryId = entity.FindProperty(nameof(Product.CategoryId));
            Assert.NotNull(categoryId);
            Assert.Equal("category_id", categoryId.GetColumnName());
            Assert.False(categoryId.IsNullable);

            var stationId = entity.FindProperty(nameof(Product.StationId));
            Assert.NotNull(stationId);
            Assert.Equal("station_id", stationId.GetColumnName());
            Assert.True(stationId.IsNullable);

            Assert.Equal("name", entity.FindProperty(nameof(Product.Name))!.GetColumnName());
            Assert.Equal("description", entity.FindProperty(nameof(Product.Description))!.GetColumnName());
            Assert.Equal("ingredients", entity.FindProperty(nameof(Product.Ingredients))!.GetColumnName());
            Assert.Equal("kind", entity.FindProperty(nameof(Product.Kind))!.GetColumnName());
            Assert.Equal("caffeine", entity.FindProperty(nameof(Product.Caffeine))!.GetColumnName());
            Assert.Equal("preparation_minutes", entity.FindProperty(nameof(Product.PreparationMinutes))!.GetColumnName());
            Assert.Equal("sort_order", entity.FindProperty(nameof(Product.SortOrder))!.GetColumnName());
            Assert.Equal("status", entity.FindProperty(nameof(Product.Status))!.GetColumnName());
            Assert.False(entity.FindProperty(nameof(Product.Name))!.IsNullable);
            Assert.True(entity.FindProperty(nameof(Product.Description))!.IsNullable);
            Assert.True(entity.FindProperty(nameof(Product.Ingredients))!.IsNullable);
            Assert.True(entity.FindProperty(nameof(Product.PreparationMinutes))!.IsNullable);

            foreach (var index in new[]
            {
                "ix_products_category_id",
                "ix_products_station_id",
                "ix_products_created_by",
                "ix_products_updated_by",
            })
            {
                Assert.Contains(entity.GetIndexes(), candidate => candidate.GetDatabaseName() == index);
            }

            var checks = entity.GetCheckConstraints().ToList();
            Assert.Contains(checks, c => c.Name == "ck_products_name_length");
            Assert.Contains(checks, c => c.Name == "ck_products_description_length");
            Assert.Contains(checks, c => c.Name == "ck_products_ingredients_length");
            Assert.Contains(checks, c => c.Name == "ck_products_kind");
            Assert.Contains(checks, c => c.Name == "ck_products_preparation_minutes");
            Assert.Contains(checks, c => c.Name == "ck_products_sort_order");
            Assert.Contains(checks, c => c.Name == "ck_products_status");

            Assert.Equal(
                typeof(MenuCategory),
                entity.GetForeignKeys().Single(fk => fk.GetConstraintName() == "fk_products_category_id").PrincipalEntityType.ClrType);
            Assert.Equal(
                typeof(Station),
                entity.GetForeignKeys().Single(fk => fk.GetConstraintName() == "fk_products_station_id").PrincipalEntityType.ClrType);
            Assert.Equal(
                typeof(TenantUser),
                entity.GetForeignKeys().Single(fk => fk.GetConstraintName() == "fk_products_created_by").PrincipalEntityType.ClrType);
            Assert.Equal(
                typeof(TenantUser),
                entity.GetForeignKeys().Single(fk => fk.GetConstraintName() == "fk_products_updated_by").PrincipalEntityType.ClrType);
            Assert.All(entity.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));

            var xmin = entity.FindProperty("xmin");
            Assert.NotNull(xmin);
            Assert.True(xmin.IsConcurrencyToken);
            Assert.False(xmin.IsNullable);
        }

        [Fact]
        public void ProductSizesTable_MatchesErd()
        {
            var model = CreateModel();
            var entity = model.FindEntityType(typeof(ProductSize));
            Assert.NotNull(entity);

            Assert.Equal("product_sizes", entity.GetTableName());

            var pk = entity.FindPrimaryKey();
            Assert.NotNull(pk);
            Assert.Equal("pk_product_sizes", pk.GetName());
            Assert.Equal("id", Assert.Single(pk.Properties).GetColumnName());

            Assert.Equal("product_id", entity.FindProperty(nameof(ProductSize.ProductId))!.GetColumnName());
            Assert.Equal("name", entity.FindProperty(nameof(ProductSize.Name))!.GetColumnName());
            Assert.Equal("price", entity.FindProperty(nameof(ProductSize.Price))!.GetColumnName());
            Assert.Equal("is_default", entity.FindProperty(nameof(ProductSize.IsDefault))!.GetColumnName());
            Assert.Equal("status", entity.FindProperty(nameof(ProductSize.Status))!.GetColumnName());
            Assert.False(entity.FindProperty(nameof(ProductSize.ProductId))!.IsNullable);

            var price = entity.FindProperty(nameof(ProductSize.Price))!;
            Assert.Equal("numeric(12,2)", price.GetColumnType());
            Assert.Equal(12, price.GetPrecision());
            Assert.Equal(2, price.GetScale());

            Assert.Contains(entity.GetIndexes(), index => index.GetDatabaseName() == "ix_product_sizes_product_id");

            var checks = entity.GetCheckConstraints().ToList();
            Assert.Contains(checks, c => c.Name == "ck_product_sizes_name_length");
            Assert.Contains(checks, c => c.Name == "ck_product_sizes_price_range");
            Assert.Contains(checks, c => c.Name == "ck_product_sizes_status");

            var productFk = entity.GetForeignKeys().Single(fk => fk.GetConstraintName() == "fk_product_sizes_product_id");
            Assert.Equal(typeof(Product), productFk.PrincipalEntityType.ClrType);
            Assert.Equal(DeleteBehavior.Restrict, productFk.DeleteBehavior);
            Assert.All(entity.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
        }

        [Fact]
        public void ProductAddonsTable_MatchesErd()
        {
            var model = CreateModel();
            var entity = model.FindEntityType(typeof(ProductAddon));
            Assert.NotNull(entity);

            Assert.Equal("product_addons", entity.GetTableName());

            var pk = entity.FindPrimaryKey();
            Assert.NotNull(pk);
            Assert.Equal("pk_product_addons", pk.GetName());
            Assert.Equal("id", Assert.Single(pk.Properties).GetColumnName());

            Assert.Equal("product_id", entity.FindProperty(nameof(ProductAddon.ProductId))!.GetColumnName());
            Assert.Equal("name", entity.FindProperty(nameof(ProductAddon.Name))!.GetColumnName());
            Assert.Equal("price", entity.FindProperty(nameof(ProductAddon.Price))!.GetColumnName());
            Assert.Equal("status", entity.FindProperty(nameof(ProductAddon.Status))!.GetColumnName());

            var price = entity.FindProperty(nameof(ProductAddon.Price))!;
            Assert.Equal("numeric(12,2)", price.GetColumnType());
            Assert.Equal(12, price.GetPrecision());
            Assert.Equal(2, price.GetScale());

            Assert.Contains(entity.GetIndexes(), index => index.GetDatabaseName() == "ix_product_addons_product_id");

            var checks = entity.GetCheckConstraints().ToList();
            Assert.Contains(checks, c => c.Name == "ck_product_addons_name_length");
            Assert.Contains(checks, c => c.Name == "ck_product_addons_price_range");
            Assert.Contains(checks, c => c.Name == "ck_product_addons_status");

            Assert.All(entity.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
        }

        [Fact]
        public void ProductTagsTable_MatchesErd()
        {
            var model = CreateModel();
            var entity = model.FindEntityType(typeof(ProductTag));
            Assert.NotNull(entity);

            Assert.Equal("product_tags", entity.GetTableName());

            var pk = entity.FindPrimaryKey();
            Assert.NotNull(pk);
            Assert.Equal("pk_product_tags", pk.GetName());
            Assert.Equal(
                new[] { "id" },
                pk.Properties.Select(property => property.GetColumnName()));

            Assert.False(entity.FindProperty(nameof(ProductTag.ProductId))!.IsNullable);
            Assert.Equal("tag", entity.FindProperty(nameof(ProductTag.Tag))!.GetColumnName());
            Assert.Equal("created_at", entity.FindProperty(nameof(ProductTag.CreatedAt))!.GetColumnName());

            Assert.Contains(entity.GetIndexes(), index => index.GetDatabaseName() == "ix_product_tags_active_tag");
            Assert.Contains(entity.GetIndexes(), index => index.GetDatabaseName() == "uq_product_tags_active_label"
                && index.IsUnique && index.GetFilter() == "status = 'Active'");

            var checks = entity.GetCheckConstraints().ToList();
            Assert.Contains(checks, c => c.Name == "ck_product_tags_tag_length");
            Assert.Contains(checks, c => c.Name == "ck_product_tags_tag_lowercase");
            Assert.Contains(checks, c => c.Name == "ck_product_tags_tag_format");

            var productFk = entity.GetForeignKeys().Single(fk => fk.GetConstraintName() == "fk_product_tags_product_id");
            Assert.Equal(typeof(Product), productFk.PrincipalEntityType.ClrType);
            Assert.Equal(DeleteBehavior.Restrict, productFk.DeleteBehavior);
        }

        [Fact]
        public void PrintJobsTable_MatchesErd()
        {
            var model = CreateModel();
            var entity = model.FindEntityType(typeof(PrintJob));
            Assert.NotNull(entity);

            Assert.Equal("print_jobs", entity.GetTableName());
            Assert.Equal("pk_print_jobs", entity.FindPrimaryKey()!.GetName());
            Assert.Equal("id", Assert.Single(entity.FindPrimaryKey()!.Properties).GetColumnName());

            Assert.Equal("branch_id", entity.FindProperty(nameof(PrintJob.BranchId))!.GetColumnName());
            Assert.Equal("printer_id", entity.FindProperty(nameof(PrintJob.PrinterId))!.GetColumnName());
            Assert.Equal("document_type", entity.FindProperty(nameof(PrintJob.DocumentType))!.GetColumnName());
            Assert.Equal("source_type", entity.FindProperty(nameof(PrintJob.SourceType))!.GetColumnName());
            Assert.Equal("source_id", entity.FindProperty(nameof(PrintJob.SourceId))!.GetColumnName());
            Assert.Equal("dedupe_key", entity.FindProperty(nameof(PrintJob.DedupeKey))!.GetColumnName());
            Assert.Equal("status", entity.FindProperty(nameof(PrintJob.Status))!.GetColumnName());
            Assert.Equal("attempts", entity.FindProperty(nameof(PrintJob.Attempts))!.GetColumnName());
            Assert.Equal("max_attempts", entity.FindProperty(nameof(PrintJob.MaxAttempts))!.GetColumnName());
            Assert.Equal("last_error_code", entity.FindProperty(nameof(PrintJob.LastErrorCode))!.GetColumnName());
            Assert.Equal("jsonb", entity.FindProperty(nameof(PrintJob.DocumentPayload))!.GetColumnType());
            Assert.Equal("lease_expires_at", entity.FindProperty(nameof(PrintJob.LeaseExpiresAt))!.GetColumnName());

            var checks = entity.GetCheckConstraints().ToList();
            foreach (var check in new[]
            {
                "ck_print_jobs_document_type",
                "ck_print_jobs_source_type",
                "ck_print_jobs_dedupe_key_length",
                "ck_print_jobs_status",
                "ck_print_jobs_attempts",
                "ck_print_jobs_max_attempts",
                "ck_print_jobs_last_error_code",
            })
            {
                Assert.Contains(checks, candidate => candidate.Name == check);
            }

            var dedupeIndex = entity.GetIndexes().Single(index =>
                index.GetDatabaseName() == "uq_print_jobs_branch_dedupe_key");
            Assert.True(dedupeIndex.IsUnique);
            Assert.Equal(
                new[] { "branch_id", "dedupe_key" },
                dedupeIndex.Properties.Select(property => property.GetColumnName()));

            Assert.Equal(
                typeof(Branch),
                entity.GetForeignKeys().Single(fk => fk.GetConstraintName() == "fk_print_jobs_branch_id").PrincipalEntityType.ClrType);
            Assert.Equal(
                typeof(Printer),
                entity.GetForeignKeys().Single(fk => fk.GetConstraintName() == "fk_print_jobs_printer_id").PrincipalEntityType.ClrType);
        }

        [Fact]
        public void PrintConnectorsTable_MatchesErd()
        {
            var model = CreateModel();
            var entity = model.FindEntityType(typeof(PrintConnector));
            Assert.NotNull(entity);

            Assert.Equal("print_connectors", entity.GetTableName());
            Assert.Equal("pk_print_connectors", entity.FindPrimaryKey()!.GetName());
            Assert.Equal("id", Assert.Single(entity.FindPrimaryKey()!.Properties).GetColumnName());

            Assert.Equal("branch_id", entity.FindProperty(nameof(PrintConnector.BranchId))!.GetColumnName());
            Assert.Equal("name", entity.FindProperty(nameof(PrintConnector.Name))!.GetColumnName());
            Assert.Equal("key_hash", entity.FindProperty(nameof(PrintConnector.KeyHash))!.GetColumnName());
            Assert.Equal("status", entity.FindProperty(nameof(PrintConnector.Status))!.GetColumnName());
            Assert.Equal("last_seen_at", entity.FindProperty(nameof(PrintConnector.LastSeenAt))!.GetColumnName());

            foreach (var check in new[]
            {
                "ck_print_connectors_name",
                "ck_print_connectors_key_hash",
                "ck_print_connectors_status",
            })
            {
                Assert.Contains(entity.GetCheckConstraints(), candidate => candidate.Name == check);
            }

            var keyHashIndex = entity.GetIndexes().Single(index =>
                index.GetDatabaseName() == "uq_print_connectors_key_hash");
            Assert.True(keyHashIndex.IsUnique);
            Assert.Equal(
                "key_hash",
                Assert.Single(keyHashIndex.Properties).GetColumnName());

            Assert.Equal(
                typeof(Branch),
                entity.GetForeignKeys().Single(fk => fk.GetConstraintName() == "fk_print_connectors_branch_id").PrincipalEntityType.ClrType);
        }

        [Fact]
        public void InventoryItemsTable_MatchesErd()
        {
            var model = CreateModel();
            var entity = model.FindEntityType(typeof(InventoryItem));
            Assert.NotNull(entity);

            Assert.Equal("inventory_items", entity.GetTableName());
            Assert.Equal("pk_inventory_items", entity.FindPrimaryKey()!.GetName());
            Assert.Equal("id", Assert.Single(entity.FindPrimaryKey()!.Properties).GetColumnName());

            Assert.Equal("name", entity.FindProperty(nameof(InventoryItem.Name))!.GetColumnName());
            Assert.Equal("unit", entity.FindProperty(nameof(InventoryItem.Unit))!.GetColumnName());
            Assert.Equal(
                "low_stock_threshold", entity.FindProperty(nameof(InventoryItem.LowStockThreshold))!.GetColumnName());
            Assert.Equal("tracks_expiry", entity.FindProperty(nameof(InventoryItem.TracksExpiry))!.GetColumnName());
            Assert.Equal("near_expiry_days", entity.FindProperty(nameof(InventoryItem.NearExpiryDays))!.GetColumnName());
            Assert.Equal("status", entity.FindProperty(nameof(InventoryItem.Status))!.GetColumnName());

            var threshold = entity.FindProperty(nameof(InventoryItem.LowStockThreshold))!;
            Assert.Equal("numeric(12,3)", threshold.GetColumnType());
            Assert.Equal(12, threshold.GetPrecision());
            Assert.Equal(3, threshold.GetScale());
            Assert.False(threshold.IsNullable);

            var nearExpiryDays = entity.FindProperty(nameof(InventoryItem.NearExpiryDays))!;
            Assert.Equal(InventoryItem.DefaultNearExpiryDays, (int)nearExpiryDays.GetDefaultValue()!);
            Assert.False(nearExpiryDays.IsNullable);

            foreach (var check in new[]
            {
                "ck_inventory_items_name",
                "ck_inventory_items_unit",
                "ck_inventory_items_low_stock_threshold",
                "ck_inventory_items_near_expiry_days",
                "ck_inventory_items_status",
            })
            {
                Assert.Contains(entity.GetCheckConstraints(), candidate => candidate.Name == check);
            }

            foreach (var index in new[]
            {
                "ix_inventory_items_created_by",
                "ix_inventory_items_updated_by",
            })
            {
                Assert.Contains(entity.GetIndexes(), candidate => candidate.GetDatabaseName() == index);
            }

            Assert.Equal(
                typeof(TenantUser),
                entity.GetForeignKeys().Single(fk => fk.GetConstraintName() == "fk_inventory_items_created_by").PrincipalEntityType.ClrType);
            Assert.Equal(
                typeof(TenantUser),
                entity.GetForeignKeys().Single(fk => fk.GetConstraintName() == "fk_inventory_items_updated_by").PrincipalEntityType.ClrType);
            Assert.All(entity.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));

            var xmin = entity.FindProperty("xmin");
            Assert.NotNull(xmin);
            Assert.True(xmin.IsConcurrencyToken);
            Assert.False(xmin.IsNullable);
        }

        [Fact]
        public void InventoryBatchesTable_MatchesErd()
        {
            var model = CreateModel();
            var entity = model.FindEntityType(typeof(InventoryBatch));
            Assert.NotNull(entity);

            Assert.Equal("inventory_batches", entity.GetTableName());
            Assert.Equal("pk_inventory_batches", entity.FindPrimaryKey()!.GetName());
            Assert.Equal("id", Assert.Single(entity.FindPrimaryKey()!.Properties).GetColumnName());

            Assert.Equal("item_id", entity.FindProperty(nameof(InventoryBatch.ItemId))!.GetColumnName());
            Assert.Equal("branch_id", entity.FindProperty(nameof(InventoryBatch.BranchId))!.GetColumnName());
            Assert.Equal(
                "received_quantity", entity.FindProperty(nameof(InventoryBatch.ReceivedQuantity))!.GetColumnName());
            Assert.Equal(
                "remaining_quantity", entity.FindProperty(nameof(InventoryBatch.RemainingQuantity))!.GetColumnName());
            Assert.Equal(
                "received_date", entity.FindProperty(nameof(InventoryBatch.ReceivedDate))!.GetColumnName());
            Assert.Equal("expiry_date", entity.FindProperty(nameof(InventoryBatch.ExpiryDate))!.GetColumnName());
            Assert.Equal(
                "received_by_user_id", entity.FindProperty(nameof(InventoryBatch.ReceivedByUserId))!.GetColumnName());
            Assert.Equal(
                "idempotency_key", entity.FindProperty(nameof(InventoryBatch.IdempotencyKey))!.GetColumnName());

            var received = entity.FindProperty(nameof(InventoryBatch.ReceivedQuantity))!;
            Assert.Equal("numeric(12,3)", received.GetColumnType());
            Assert.Equal(3, received.GetScale());
            var remaining = entity.FindProperty(nameof(InventoryBatch.RemainingQuantity))!;
            Assert.Equal("numeric(12,3)", remaining.GetColumnType());
            Assert.Equal(3, remaining.GetScale());
            Assert.Equal("date", entity.FindProperty(nameof(InventoryBatch.ReceivedDate))!.GetColumnType());
            Assert.True(entity.FindProperty(nameof(InventoryBatch.ExpiryDate))!.IsNullable);

            foreach (var check in new[]
            {
                "ck_inventory_batches_received_quantity",
                "ck_inventory_batches_remaining_quantity",
                "ck_inventory_batches_expiry_not_before_received",
                "ck_inventory_batches_idempotency_key_length",
            })
            {
                Assert.Contains(entity.GetCheckConstraints(), candidate => candidate.Name == check);
            }

            foreach (var index in new[]
            {
                "uq_inventory_batches_branch_idempotency_key",
                "ix_inventory_batches_item_id",
                "ix_inventory_batches_received_by_user_id",
                "ix_inventory_batches_created_by",
                "ix_inventory_batches_updated_by",
            })
            {
                Assert.Contains(entity.GetIndexes(), candidate => candidate.GetDatabaseName() == index);
            }

            Assert.True(entity.GetIndexes()
                .Single(candidate => candidate.GetDatabaseName() == "uq_inventory_batches_branch_idempotency_key")
                .IsUnique);
            Assert.Equal(
                typeof(InventoryItem),
                entity.GetForeignKeys().Single(fk => fk.GetConstraintName() == "fk_inventory_batches_item_id").PrincipalEntityType.ClrType);
            Assert.Equal(
                typeof(Branch),
                entity.GetForeignKeys().Single(fk => fk.GetConstraintName() == "fk_inventory_batches_branch_id").PrincipalEntityType.ClrType);
            Assert.Equal(
                typeof(TenantUser),
                entity.GetForeignKeys().Single(fk => fk.GetConstraintName() == "fk_inventory_batches_received_by_user_id").PrincipalEntityType.ClrType);
            Assert.All(entity.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));

            var xmin = entity.FindProperty("xmin");
            Assert.NotNull(xmin);
            Assert.True(xmin.IsConcurrencyToken);
        }
    }
}
