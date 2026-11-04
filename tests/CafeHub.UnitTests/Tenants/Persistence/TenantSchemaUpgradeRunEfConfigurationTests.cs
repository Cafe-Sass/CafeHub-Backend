using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Master;
using CafeHub.Infrastructure.Persistence.Master.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CafeHub.UnitTests.Tenants.Persistence
{
    public sealed class TenantSchemaUpgradeRunEfConfigurationTests
    {
        private static MasterDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<MasterDbContext>()
                .UseNpgsql("Host=localhost;Database=test")
                .Options;

            return new MasterDbContext(options);
        }

        private static IEntityType FindEntity()
        {
            using var context = CreateContext();
            var entity = context.GetService<IDesignTimeModel>().Model
                .FindEntityType(typeof(TenantSchemaUpgradeRun));
            Assert.NotNull(entity);
            return entity!;
        }

        private static void AssertColumn(
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
        }

        private static IIndex FindIndex(IEntityType entity, string databaseName)
        {
            return entity.GetIndexes().Single(index => index.GetDatabaseName() == databaseName);
        }

        [Fact]
        public void TableAndColumns_MatchTheErd()
        {
            var entity = FindEntity();

            Assert.Equal("tenant_schema_upgrade_runs", entity.GetTableName());

            var primaryKey = entity.FindPrimaryKey();
            Assert.NotNull(primaryKey);
            Assert.Equal("pk_tenant_schema_upgrade_runs", primaryKey!.GetName());
            var keyProperty = Assert.Single(primaryKey.Properties);
            Assert.Equal("id", keyProperty.GetColumnName());
            Assert.Equal(typeof(Guid), keyProperty.ClrType);
            Assert.False(keyProperty.IsNullable);

            AssertColumn(entity, nameof(TenantSchemaUpgradeRun.Id), "id", typeof(Guid), nullable: false);
            AssertColumn(entity, nameof(TenantSchemaUpgradeRun.TenantId), "tenant_id", typeof(Guid), nullable: false);
            AssertColumn(entity, nameof(TenantSchemaUpgradeRun.TargetMigrationId), "target_migration_id", typeof(string), nullable: false);
            AssertColumn(entity, nameof(TenantSchemaUpgradeRun.Status), "status", typeof(TenantSchemaUpgradeRunStatus), nullable: false);
            AssertColumn(entity, nameof(TenantSchemaUpgradeRun.FailureCode), "failure_code", typeof(string), nullable: true);
            AssertColumn(entity, nameof(TenantSchemaUpgradeRun.AttemptCount), "attempt_count", typeof(int), nullable: false);
            AssertColumn(entity, nameof(TenantSchemaUpgradeRun.RequestedBy), "requested_by", typeof(Guid?), nullable: true);
            AssertColumn(entity, nameof(TenantSchemaUpgradeRun.StartedAt), "started_at", typeof(DateTimeOffset?), nullable: true);
            AssertColumn(entity, nameof(TenantSchemaUpgradeRun.FinishedAt), "finished_at", typeof(DateTimeOffset?), nullable: true);
            AssertColumn(entity, nameof(TenantSchemaUpgradeRun.LeaseExpiresAt), "lease_expires_at", typeof(DateTimeOffset?), nullable: true);
            AssertColumn(entity, nameof(TenantSchemaUpgradeRun.LeasedBy), "leased_by", typeof(string), nullable: true);
            AssertColumn(entity, nameof(TenantSchemaUpgradeRun.CreatedAt), "created_at", typeof(DateTimeOffset), nullable: false);
            AssertColumn(entity, nameof(TenantSchemaUpgradeRun.UpdatedAt), "updated_at", typeof(DateTimeOffset), nullable: false);
            AssertColumn(entity, nameof(TenantSchemaUpgradeRun.CreatedBy), "created_by", typeof(Guid?), nullable: true);
            AssertColumn(entity, nameof(TenantSchemaUpgradeRun.UpdatedBy), "updated_by", typeof(Guid?), nullable: true);
        }

        [Fact]
        public void Indexes_MatchTheErd()
        {
            var entity = FindEntity();

            var tenantIndex = FindIndex(entity, "ix_tenant_schema_upgrade_runs_tenant_id");
            Assert.False(tenantIndex.IsUnique);
            Assert.Equal(nameof(TenantSchemaUpgradeRun.TenantId), tenantIndex.Properties.Single().Name);
            Assert.Null(tenantIndex.GetFilter());

            var activeIndex = FindIndex(entity, "uq_tenant_schema_upgrade_runs_active");
            Assert.True(activeIndex.IsUnique);
            Assert.Equal(nameof(TenantSchemaUpgradeRun.TenantId), activeIndex.Properties.Single().Name);
            Assert.Equal("status IN ('Pending', 'Running')", activeIndex.GetFilter());

            Assert.Contains(entity.GetIndexes(), index => index.GetDatabaseName() == "IX_tenant_schema_upgrade_runs_requested_by");
            Assert.Contains(entity.GetIndexes(), index => index.GetDatabaseName() == "IX_tenant_schema_upgrade_runs_created_by");
            Assert.Contains(entity.GetIndexes(), index => index.GetDatabaseName() == "IX_tenant_schema_upgrade_runs_updated_by");
        }

        [Fact]
        public void CheckConstraints_MatchTheErd()
        {
            var entity = FindEntity();
            var checks = entity.GetCheckConstraints().ToDictionary(constraint => constraint.Name!, constraint => constraint.Sql);

            Assert.Equal(4, checks.Count);
            Assert.Equal(
                "status IN ('Pending', 'Running', 'Succeeded', 'Failed')",
                checks["ck_tenant_schema_upgrade_runs_status"]);
            Assert.Equal(
                "length(target_migration_id) >= 1 AND length(target_migration_id) <= 150",
                checks["ck_tenant_schema_upgrade_runs_target_migration_id_length"]);
            Assert.Equal(
                "failure_code IS NULL OR (length(failure_code) >= 1 AND length(failure_code) <= 64)",
                checks["ck_tenant_schema_upgrade_runs_failure_code_length"]);
            Assert.Equal(
                "attempt_count >= 0",
                checks["ck_tenant_schema_upgrade_runs_attempt_count_not_negative"]);
        }

        [Fact]
        public void ForeignKeys_MatchTheErd()
        {
            var entity = FindEntity();
            var foreignKeys = entity.GetForeignKeys().ToDictionary(foreignKey => foreignKey.GetConstraintName()!);

            Assert.Equal(4, foreignKeys.Count);

            var tenantForeignKey = foreignKeys["fk_tenant_schema_upgrade_runs_tenant_id"];
            Assert.Equal(typeof(Tenant), tenantForeignKey.PrincipalEntityType.ClrType);
            Assert.True(tenantForeignKey.IsRequired);
            Assert.Equal(DeleteBehavior.Restrict, tenantForeignKey.DeleteBehavior);

            foreach (var name in new[]
                     {
                         "fk_tenant_schema_upgrade_runs_requested_by",
                         "fk_tenant_schema_upgrade_runs_created_by",
                         "fk_tenant_schema_upgrade_runs_updated_by",
                     })
            {
                var actorForeignKey = foreignKeys[name];
                Assert.Equal(typeof(PlatformAdminUser), actorForeignKey.PrincipalEntityType.ClrType);
                Assert.False(actorForeignKey.IsRequired);
                Assert.Equal(DeleteBehavior.Restrict, actorForeignKey.DeleteBehavior);
            }
        }

        [Fact]
        public void Migration_ContainsOnlyTheIntendedDdl()
        {
            using var context = CreateContext();
            var migrations = context.GetService<IMigrationsAssembly>();
            var migrationType = migrations.Migrations
                .Single(migration => migration.Key.EndsWith("_AddTenantSchemaUpgradeTracking", StringComparison.Ordinal))
                .Value;
            var migration = migrations.CreateMigration(migrationType, context.Database.ProviderName!);

            var createTable = Assert.Single(migration.UpOperations.OfType<CreateTableOperation>());
            Assert.Equal("tenant_schema_upgrade_runs", createTable.Name);
            Assert.Equal("pk_tenant_schema_upgrade_runs", createTable.PrimaryKey!.Name);
            Assert.Equal(4, createTable.ForeignKeys.Count);

            var addColumn = Assert.Single(migration.UpOperations.OfType<AddColumnOperation>());
            Assert.Equal("schema_migration_id", addColumn.Name);
            Assert.Equal("tenant_databases", addColumn.Table);
            Assert.Equal("text", addColumn.ColumnType);
            Assert.True(addColumn.IsNullable);

            var addCheck = Assert.Single(migration.UpOperations.OfType<AddCheckConstraintOperation>());
            Assert.Equal("ck_tenant_databases_schema_migration_id_length", addCheck.Name);
            Assert.Equal("tenant_databases", addCheck.Table);

            var indexes = migration.UpOperations.OfType<CreateIndexOperation>().ToDictionary(operation => operation.Name!);

            Assert.Equal(5, indexes.Count);
            Assert.All(indexes.Values, index => Assert.Equal("tenant_schema_upgrade_runs", index.Table));
            Assert.True(indexes["uq_tenant_schema_upgrade_runs_active"].IsUnique);
            Assert.Equal("status IN ('Pending', 'Running')", indexes["uq_tenant_schema_upgrade_runs_active"].Filter);

            foreach (var name in new[]
                     {
                         "IX_tenant_schema_upgrade_runs_created_by",
                         "IX_tenant_schema_upgrade_runs_requested_by",
                         "ix_tenant_schema_upgrade_runs_tenant_id",
                         "IX_tenant_schema_upgrade_runs_updated_by",
                     })
            {
                var index = indexes[name];
                Assert.False(index.IsUnique);
                Assert.Null(index.Filter);
                Assert.Single(index.Columns);
            }

            Assert.Equal("created_by", Assert.Single(indexes["IX_tenant_schema_upgrade_runs_created_by"].Columns));
            Assert.Equal("requested_by", Assert.Single(indexes["IX_tenant_schema_upgrade_runs_requested_by"].Columns));
            Assert.Equal("tenant_id", Assert.Single(indexes["ix_tenant_schema_upgrade_runs_tenant_id"].Columns));
            Assert.Equal("updated_by", Assert.Single(indexes["IX_tenant_schema_upgrade_runs_updated_by"].Columns));

            Assert.Equal(8, migration.UpOperations.Count);

            Assert.Single(migration.DownOperations.OfType<DropTableOperation>());
            var dropCheck = Assert.Single(migration.DownOperations.OfType<DropCheckConstraintOperation>());
            Assert.Equal("ck_tenant_databases_schema_migration_id_length", dropCheck.Name);
            var dropColumn = Assert.Single(migration.DownOperations.OfType<DropColumnOperation>());
            Assert.Equal("schema_migration_id", dropColumn.Name);
            Assert.Equal(3, migration.DownOperations.Count);
        }
    }
}
