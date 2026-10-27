using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Master;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CafeHub.UnitTests.Tenants.Persistence
{
    public sealed class TenantRefreshTokenEfConfigurationTests
    {
        private static TenantDbContext CreateTenantContext()
        {
            var options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseNpgsql("Host=localhost;Database=test")
                .Options;
            return new TenantDbContext(options);
        }

        private static IEntityType FindEntity()
        {
            using var context = CreateTenantContext();
            var entity = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(TenantRefreshToken));
            Assert.NotNull(entity);
            return entity!;
        }

        [Fact]
        public void TableAndColumns_MatchTheErd()
        {
            var entity = FindEntity();

            Assert.Equal("refresh_tokens", entity.GetTableName());
            var primaryKey = entity.FindPrimaryKey();
            Assert.NotNull(primaryKey);
            Assert.Equal("pk_refresh_tokens", primaryKey.GetName());
            AssertColumn(entity, nameof(TenantRefreshToken.Id), "id", typeof(Guid), false);
            AssertColumn(entity, nameof(TenantRefreshToken.UserId), "user_id", typeof(Guid), false);
            AssertColumn(entity, nameof(TenantRefreshToken.FamilyId), "family_id", typeof(Guid), false);
            AssertColumn(entity, nameof(TenantRefreshToken.TokenHash), "token_hash", typeof(string), false);
            AssertColumn(entity, nameof(TenantRefreshToken.IssuedAt), "issued_at", typeof(DateTimeOffset), false);
            AssertColumn(entity, nameof(TenantRefreshToken.ExpiresAt), "expires_at", typeof(DateTimeOffset), false);
            AssertColumn(entity, nameof(TenantRefreshToken.RotatedAt), "rotated_at", typeof(DateTimeOffset?), true);
            AssertColumn(entity, nameof(TenantRefreshToken.ReplacedById), "replaced_by_id", typeof(Guid?), true);
            AssertColumn(entity, nameof(TenantRefreshToken.RevokedAt), "revoked_at", typeof(DateTimeOffset?), true);
            AssertColumn(entity, nameof(TenantRefreshToken.RevokedReason), "revoked_reason", typeof(string), true);
            var concurrencyToken = entity.FindProperty("xmin");
            Assert.NotNull(concurrencyToken);
            Assert.True(concurrencyToken.IsConcurrencyToken);
            Assert.Equal(ValueGenerated.OnAddOrUpdate, concurrencyToken.ValueGenerated);
        }

        [Fact]
        public void IndexesAndCheckConstraints_MatchTheErd()
        {
            var entity = FindEntity();

            AssertIndex(entity, nameof(TenantRefreshToken.TokenHash), "uq_refresh_tokens_token_hash", unique: true);
            AssertIndex(entity, nameof(TenantRefreshToken.FamilyId), "ix_refresh_tokens_family_id", unique: false);
            AssertIndex(entity, nameof(TenantRefreshToken.UserId), "ix_refresh_tokens_user_id", unique: false);
            AssertIndex(entity, nameof(TenantRefreshToken.ReplacedById), "uq_refresh_tokens_replaced_by_id", unique: true);

            var constraints = entity.GetCheckConstraints().ToDictionary(constraint => constraint.Name!, constraint => constraint.Sql);
            Assert.Equal(5, constraints.Count);
            Assert.Equal("token_hash ~ '^[0-9a-f]{64}$'", constraints["ck_refresh_tokens_token_hash_format"]);
            Assert.Equal("expires_at > issued_at", constraints["ck_refresh_tokens_expiry_after_issue"]);
            Assert.Equal(
                "revoked_reason IS NULL OR (length(revoked_reason) >= 1 AND length(revoked_reason) <= 64)",
                constraints["ck_refresh_tokens_revoked_reason_length"]);
            Assert.Equal(
                "revoked_reason IS NULL OR revoked_reason IN ('logout', 'reuse_detected', 'admin_revoked')",
                constraints["ck_refresh_tokens_revoked_reason_labels"]);
            Assert.Equal(
                "(revoked_at IS NULL AND revoked_reason IS NULL) OR (revoked_at IS NOT NULL AND revoked_reason IS NOT NULL)",
                constraints["ck_refresh_tokens_revocation_pair"]);
        }

        [Fact]
        public void ForeignKeys_AreTenantLocalAndRestrictDeletion()
        {
            var entity = FindEntity();

            var userForeignKey = entity.GetForeignKeys().Single(foreignKey => foreignKey.GetConstraintName() == "fk_refresh_tokens_user_id");
            Assert.Equal(typeof(TenantUser), userForeignKey.PrincipalEntityType.ClrType);
            Assert.Equal(DeleteBehavior.Restrict, userForeignKey.DeleteBehavior);

            var replacementForeignKey = entity.GetForeignKeys().Single(foreignKey => foreignKey.GetConstraintName() == "fk_refresh_tokens_replaced_by_id");
            Assert.Equal(typeof(TenantRefreshToken), replacementForeignKey.PrincipalEntityType.ClrType);
            Assert.Equal(DeleteBehavior.Restrict, replacementForeignKey.DeleteBehavior);
        }

        [Fact]
        public void MasterModel_DoesNotContainTenantRefreshToken()
        {
            var options = new DbContextOptionsBuilder<MasterDbContext>()
                .UseNpgsql("Host=localhost;Database=test")
                .Options;
            using var context = new MasterDbContext(options);

            Assert.Null(context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(TenantRefreshToken)));
        }

        [Fact]
        public void AddTenantRefreshTokensMigration_CreatesOnlyTheRefreshTokenTableAndIndexes()
        {
            using var context = CreateTenantContext();
            var migrations = context.GetService<IMigrationsAssembly>();
            var migrationType = migrations.Migrations.Single(migration => migration.Key.EndsWith("_AddTenantRefreshTokens", StringComparison.Ordinal)).Value;
            var migration = migrations.CreateMigration(migrationType, context.Database.ProviderName!);
            var createTable = Assert.Single(migration.UpOperations.OfType<CreateTableOperation>());
            var indexes = migration.UpOperations.OfType<CreateIndexOperation>().ToList();

            Assert.Equal("refresh_tokens", createTable.Name);
            Assert.Equal(4, indexes.Count);
            Assert.All(indexes, index => Assert.Equal("refresh_tokens", index.Table));
            Assert.Equal(5, migration.UpOperations.Count);
        }

        [Fact]
        public void AddTenantRefreshTokenConcurrencyTokenMigration_UsesPostgresRowVersion()
        {
            using var context = CreateTenantContext();
            var migrations = context.GetService<IMigrationsAssembly>();
            var migrationType = migrations.Migrations.Single(migration => migration.Key.EndsWith("_AddTenantRefreshTokenConcurrencyToken", StringComparison.Ordinal)).Value;
            var migration = migrations.CreateMigration(migrationType, context.Database.ProviderName!);
            var addColumn = Assert.Single(migration.UpOperations.OfType<AddColumnOperation>());

            Assert.Equal("xmin", addColumn.Name);
            Assert.Equal("refresh_tokens", addColumn.Table);
            Assert.Equal("xid", addColumn.ColumnType);
            Assert.True(addColumn.IsRowVersion);
        }

        private static void AssertColumn(IEntityType entity, string propertyName, string columnName, Type clrType, bool nullable)
        {
            var property = entity.FindProperty(propertyName);
            Assert.NotNull(property);
            Assert.Equal(columnName, property.GetColumnName());
            Assert.Equal(clrType, property.ClrType);
            Assert.Equal(nullable, property.IsNullable);
        }

        private static void AssertIndex(IEntityType entity, string propertyName, string databaseName, bool unique)
        {
            var index = entity.GetIndexes().Single(item => item.Properties.Any(property => property.Name == propertyName));
            Assert.Equal(unique, index.IsUnique);
            Assert.Equal(databaseName, index.GetDatabaseName());
        }
    }
}
