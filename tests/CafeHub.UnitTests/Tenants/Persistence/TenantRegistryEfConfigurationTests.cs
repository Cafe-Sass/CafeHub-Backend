using System;
using System.Linq;
using CafeHub.Domain.Onboarding;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Master;
using CafeHub.Infrastructure.Persistence.Master.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace CafeHub.UnitTests.Tenants.Persistence
{
    public sealed class TenantRegistryEfConfigurationTests
    {
        private static IModel CreateModel()
        {
            var options = new DbContextOptionsBuilder<MasterDbContext>()
                .UseNpgsql("Host=localhost;Database=test")
                .Options;

            using var context = new MasterDbContext(options);
            return context.GetService<IDesignTimeModel>().Model;
        }

        private static IEntityType FindEntity<TEntity>()
        {
            var entity = CreateModel().FindEntityType(typeof(TEntity));
            Assert.NotNull(entity);
            return entity!;
        }

        private static IProperty AssertColumn(
            IEntityType entity,
            string propertyName,
            string columnName,
            Type clrType,
            bool nullable)
        {
            var property = entity.FindProperty(propertyName);
            Assert.NotNull(property);
            Assert.Equal(columnName, property!.GetColumnName());
            Assert.Equal(clrType, property.ClrType);
            Assert.Equal(nullable, property.IsNullable);
            return property;
        }

        private static IIndex FindIndex(IEntityType entity, params string[] propertyNames)
        {
            return entity.GetIndexes().Single(index =>
                index.Properties.Select(property => property.Name).SequenceEqual(propertyNames));
        }

        [Fact]
        public void TenantsTable_MatchesTheErd()
        {
            var entity = FindEntity<Tenant>();

            Assert.Equal("tenants", entity.GetTableName());

            var primaryKey = entity.FindPrimaryKey();
            Assert.NotNull(primaryKey);
            Assert.Equal("pk_tenants", primaryKey!.GetName());
            var keyProperty = Assert.Single(primaryKey.Properties);
            Assert.Equal("id", keyProperty.GetColumnName());
            Assert.Equal(typeof(Guid), keyProperty.ClrType);
            Assert.False(keyProperty.IsNullable);
        }

        [Fact]
        public void TenantsColumns_MatchTheErd()
        {
            var entity = FindEntity<Tenant>();

            AssertColumn(entity, nameof(Tenant.Id), "id", typeof(Guid), nullable: false);
            AssertColumn(entity, nameof(Tenant.OnboardingRequestId), "onboarding_request_id", typeof(Guid), nullable: false);
            AssertColumn(entity, nameof(Tenant.Slug), "slug", typeof(string), nullable: false);
            AssertColumn(entity, nameof(Tenant.LifecycleStatus), "lifecycle_status", typeof(TenantLifecycleStatus), nullable: false);
            AssertColumn(entity, nameof(Tenant.CreatedAt), "created_at", typeof(DateTimeOffset), nullable: false);
            AssertColumn(entity, nameof(Tenant.UpdatedAt), "updated_at", typeof(DateTimeOffset), nullable: false);
            AssertColumn(entity, nameof(Tenant.CreatedBy), "created_by", typeof(Guid?), nullable: true);
            AssertColumn(entity, nameof(Tenant.UpdatedBy), "updated_by", typeof(Guid?), nullable: true);
        }

        [Fact]
        public void TenantsUniqueIndexes_MatchTheErd()
        {
            var entity = FindEntity<Tenant>();

            var slugIndex = FindIndex(entity, nameof(Tenant.Slug));
            Assert.True(slugIndex.IsUnique);
            Assert.Equal("uq_tenants_slug", slugIndex.GetDatabaseName());
            Assert.Null(slugIndex.GetFilter());

            var requestIndex = FindIndex(entity, nameof(Tenant.OnboardingRequestId));
            Assert.True(requestIndex.IsUnique);
            Assert.Equal("uq_tenants_onboarding_request_id", requestIndex.GetDatabaseName());
            Assert.Null(requestIndex.GetFilter());
        }

        [Fact]
        public void TenantsCheckConstraints_MatchTheErd()
        {
            var entity = FindEntity<Tenant>();
            var checks = entity.GetCheckConstraints().ToDictionary(c => c.Name!, c => c.Sql);

            Assert.Contains(checks, c => c.Key == "ck_tenants_slug_format" && c.Value == "slug ~ '^[a-z0-9][a-z0-9-]{1,38}[a-z0-9]$'");
            Assert.Contains(checks, c => c.Key == "ck_tenants_slug_lowercase" && c.Value == "slug = lower(slug)");
            Assert.Contains(checks, c => c.Key == "ck_tenants_slug_reserved" && c.Value == "slug NOT IN ('admin','api','t','www','app','static','assets','health','login','signup','support','platform')");
            Assert.Contains(checks, c => c.Key == "ck_tenants_lifecycle_status" && c.Value == "lifecycle_status IN ('Provisioning', 'Active', 'Suspended', 'Terminated')");
        }

        [Fact]
        public void TenantsForeignKeys_MatchTheErd()
        {
            var entity = FindEntity<Tenant>();

            Assert.Equal(3, entity.GetForeignKeys().Count());
            var requestFk = entity.GetForeignKeys().Single(fk => fk.GetConstraintName() == "fk_tenants_onboarding_request_id");
            Assert.Equal(typeof(OnboardingRequest), requestFk.PrincipalEntityType.ClrType);
            Assert.True(requestFk.IsRequired);
            Assert.Equal(DeleteBehavior.Restrict, requestFk.DeleteBehavior);

            var createdByFk = entity.GetForeignKeys().Single(fk => fk.GetConstraintName() == "fk_tenants_created_by");
            Assert.Equal(typeof(PlatformAdminUser), createdByFk.PrincipalEntityType.ClrType);
            Assert.False(createdByFk.IsRequired);
            Assert.Equal(DeleteBehavior.Restrict, createdByFk.DeleteBehavior);

            var updatedByFk = entity.GetForeignKeys().Single(fk => fk.GetConstraintName() == "fk_tenants_updated_by");
            Assert.Equal(typeof(PlatformAdminUser), updatedByFk.PrincipalEntityType.ClrType);
            Assert.False(updatedByFk.IsRequired);
            Assert.Equal(DeleteBehavior.Restrict, updatedByFk.DeleteBehavior);
        }

        [Fact]
        public void TenantDatabasesTable_MatchesTheErd()
        {
            var entity = FindEntity<TenantDatabase>();

            Assert.Equal("tenant_databases", entity.GetTableName());

            var primaryKey = entity.FindPrimaryKey();
            Assert.NotNull(primaryKey);
            Assert.Equal("pk_tenant_databases", primaryKey!.GetName());
            var keyProperty = Assert.Single(primaryKey.Properties);
            Assert.Equal("id", keyProperty.GetColumnName());
            Assert.Equal(typeof(Guid), keyProperty.ClrType);
            Assert.False(keyProperty.IsNullable);
        }

        [Fact]
        public void TenantDatabasesColumns_MatchTheErd()
        {
            var entity = FindEntity<TenantDatabase>();

            AssertColumn(entity, nameof(TenantDatabase.Id), "id", typeof(Guid), nullable: false);
            AssertColumn(entity, nameof(TenantDatabase.TenantId), "tenant_id", typeof(Guid), nullable: false);
            AssertColumn(entity, nameof(TenantDatabase.SecretReference), "secret_reference", typeof(string), nullable: false);
            AssertColumn(entity, nameof(TenantDatabase.Status), "status", typeof(TenantDatabaseStatus), nullable: false);
            AssertColumn(entity, nameof(TenantDatabase.CreatedAt), "created_at", typeof(DateTimeOffset), nullable: false);
            AssertColumn(entity, nameof(TenantDatabase.UpdatedAt), "updated_at", typeof(DateTimeOffset), nullable: false);
            AssertColumn(entity, nameof(TenantDatabase.CreatedBy), "created_by", typeof(Guid?), nullable: true);
            AssertColumn(entity, nameof(TenantDatabase.UpdatedBy), "updated_by", typeof(Guid?), nullable: true);
        }

        [Fact]
        public void TenantDatabasesUniqueIndexes_MatchTheErd()
        {
            var entity = FindEntity<TenantDatabase>();

            var tenantIndex = FindIndex(entity, nameof(TenantDatabase.TenantId));
            Assert.True(tenantIndex.IsUnique);
            Assert.Equal("uq_tenant_databases_tenant_id", tenantIndex.GetDatabaseName());
            Assert.Null(tenantIndex.GetFilter());

            var secretIndex = FindIndex(entity, nameof(TenantDatabase.SecretReference));
            Assert.True(secretIndex.IsUnique);
            Assert.Equal("uq_tenant_databases_secret_reference", secretIndex.GetDatabaseName());
            Assert.Null(secretIndex.GetFilter());
        }

        [Fact]
        public void TenantDatabasesCheckConstraints_MatchTheErd()
        {
            var entity = FindEntity<TenantDatabase>();
            var checks = entity.GetCheckConstraints().ToDictionary(c => c.Name!, c => c.Sql);

            Assert.Contains(checks, c => c.Key == "ck_tenant_databases_secret_reference_format" && c.Value == "secret_reference ~ '^[a-z0-9][a-z0-9-]*$'");
            Assert.Contains(checks, c => c.Key == "ck_tenant_databases_secret_reference_length" && c.Value == "length(secret_reference) >= 1 AND length(secret_reference) <= 128");
            Assert.Contains(checks, c => c.Key == "ck_tenant_databases_status" && c.Value == "status IN ('Pending', 'Provisioned', 'Failed')");
        }

        [Fact]
        public void TenantDatabasesForeignKeys_MatchTheErd()
        {
            var entity = FindEntity<TenantDatabase>();

            Assert.Equal(3, entity.GetForeignKeys().Count());
            var tenantFk = entity.GetForeignKeys().Single(fk => fk.GetConstraintName() == "fk_tenant_databases_tenant_id");
            Assert.Equal(typeof(Tenant), tenantFk.PrincipalEntityType.ClrType);
            Assert.True(tenantFk.IsRequired);
            Assert.Equal(DeleteBehavior.Restrict, tenantFk.DeleteBehavior);

            var createdByFk = entity.GetForeignKeys().Single(fk => fk.GetConstraintName() == "fk_tenant_databases_created_by");
            Assert.Equal(typeof(PlatformAdminUser), createdByFk.PrincipalEntityType.ClrType);
            Assert.False(createdByFk.IsRequired);
            Assert.Equal(DeleteBehavior.Restrict, createdByFk.DeleteBehavior);

            var updatedByFk = entity.GetForeignKeys().Single(fk => fk.GetConstraintName() == "fk_tenant_databases_updated_by");
            Assert.Equal(typeof(PlatformAdminUser), updatedByFk.PrincipalEntityType.ClrType);
            Assert.False(updatedByFk.IsRequired);
            Assert.Equal(DeleteBehavior.Restrict, updatedByFk.DeleteBehavior);
        }

        [Fact]
        public void TenantDatabaseModel_HasNoConnectionStringOrPasswordColumns()
        {
            var entity = FindEntity<TenantDatabase>();
            var columnNames = entity.GetProperties().Select(p => p.GetColumnName()).ToHashSet();

            Assert.DoesNotContain("connection_string", columnNames);
            Assert.DoesNotContain("connectionstring", columnNames);
            Assert.DoesNotContain("password", columnNames);
            Assert.DoesNotContain("pwd", columnNames);
            Assert.DoesNotContain("secret", columnNames);
            Assert.DoesNotContain("token", columnNames);
        }

        [Fact]
        public void TenantModel_HasNoSecretColumns()
        {
            var entity = FindEntity<Tenant>();
            var columnNames = entity.GetProperties().Select(p => p.GetColumnName()).ToHashSet();

            Assert.DoesNotContain("connection_string", columnNames);
            Assert.DoesNotContain("password", columnNames);
            Assert.DoesNotContain("pwd", columnNames);
            Assert.DoesNotContain("secret", columnNames);
            Assert.DoesNotContain("token", columnNames);
        }
    }
}
