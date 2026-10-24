using System;
using System.Linq;
using CafeHub.Domain.Identity;
using CafeHub.Infrastructure.Persistence.Master;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace CafeHub.UnitTests
{
    public sealed class RefreshTokenEfConfigurationTests
    {
        private static IModel CreateModel()
        {
            var options = new DbContextOptionsBuilder<MasterDbContext>()
                .UseNpgsql("Host=localhost;Database=test")
                .Options;

            using var context = new MasterDbContext(options);
            return context.GetService<IDesignTimeModel>().Model;
        }

        private static IEntityType FindRefreshTokenEntity()
        {
            var model = CreateModel();
            var entity = model.FindEntityType(typeof(RefreshToken));
            Assert.NotNull(entity);
            return entity!;
        }

        [Fact]
        public void RefreshTokensTable_MatchesTheErdName_AndPrimaryKey()
        {
            var entity = FindRefreshTokenEntity();

            Assert.Equal("refresh_tokens", entity.GetTableName());

            var primaryKey = entity.FindPrimaryKey();
            Assert.NotNull(primaryKey);
            Assert.Equal("pk_refresh_tokens", primaryKey.GetName());
            var keyProperty = Assert.Single(primaryKey.Properties);
            Assert.Equal("id", keyProperty.GetColumnName());
            Assert.Equal(typeof(Guid), keyProperty.ClrType);
            Assert.False(keyProperty.IsNullable);
        }

        [Fact]
        public void RefreshTokensColumns_MatchTheErd()
        {
            var entity = FindRefreshTokenEntity();

            AssertColumn(entity, nameof(RefreshToken.AdminId), "admin_id", typeof(Guid), nullable: false);
            AssertColumn(entity, nameof(RefreshToken.FamilyId), "family_id", typeof(Guid), nullable: false);
            AssertColumn(entity, nameof(RefreshToken.TokenHash), "token_hash", typeof(string), nullable: false);
            AssertColumn(entity, nameof(RefreshToken.IssuedAt), "issued_at", typeof(DateTimeOffset), nullable: false);
            AssertColumn(entity, nameof(RefreshToken.ExpiresAt), "expires_at", typeof(DateTimeOffset), nullable: false);
            AssertColumn(entity, nameof(RefreshToken.RotatedAt), "rotated_at", typeof(DateTimeOffset?), nullable: true);
            AssertColumn(entity, nameof(RefreshToken.ReplacedById), "replaced_by_id", typeof(Guid?), nullable: true);
            AssertColumn(entity, nameof(RefreshToken.RevokedAt), "revoked_at", typeof(DateTimeOffset?), nullable: true);
            AssertColumn(entity, nameof(RefreshToken.RevokedReason), "revoked_reason", typeof(string), nullable: true);
        }

        private static void AssertColumn(IEntityType entity, string propertyName, string columnName, Type clrType, bool nullable)
        {
            var property = entity.FindProperty(propertyName);
            Assert.NotNull(property);
            Assert.Equal(columnName, property.GetColumnName());
            Assert.Equal(clrType, property.ClrType);
            Assert.Equal(nullable, property.IsNullable);
        }

        [Fact]
        public void RefreshTokensIndexes_MatchTheErd()
        {
            var entity = FindRefreshTokenEntity();

            var tokenHashIndex = entity.GetIndexes().Single(i => i.Properties.Any(p => p.Name == nameof(RefreshToken.TokenHash)));
            Assert.True(tokenHashIndex.IsUnique);
            Assert.Equal("uq_refresh_tokens_token_hash", tokenHashIndex.GetDatabaseName());

            var familyIdIndex = entity.GetIndexes().Single(i => i.Properties.Any(p => p.Name == nameof(RefreshToken.FamilyId)));
            Assert.False(familyIdIndex.IsUnique);
            Assert.Equal("ix_refresh_tokens_family_id", familyIdIndex.GetDatabaseName());

            var adminIdIndex = entity.GetIndexes().Single(i => i.Properties.Any(p => p.Name == nameof(RefreshToken.AdminId)));
            Assert.False(adminIdIndex.IsUnique);
            Assert.Equal("ix_refresh_tokens_admin_id", adminIdIndex.GetDatabaseName());

            var replacedByIdIndex = entity.GetIndexes().Single(i => i.Properties.Any(p => p.Name == nameof(RefreshToken.ReplacedById)));
            Assert.True(replacedByIdIndex.IsUnique);
            Assert.Equal("uq_refresh_tokens_replaced_by_id", replacedByIdIndex.GetDatabaseName());
        }

        [Fact]
        public void RefreshTokensCheckConstraints_MatchTheErd()
        {
            var entity = FindRefreshTokenEntity();

            var checkConstraints = entity.GetCheckConstraints()
                .ToDictionary(constraint => constraint.Name, constraint => constraint.Sql);

            Assert.Equal(5, checkConstraints.Count);
            Assert.Equal(
                "token_hash ~ '^[0-9a-f]{64}$'",
                checkConstraints["ck_refresh_tokens_token_hash_format"]);
            Assert.Equal(
                "expires_at > issued_at",
                checkConstraints["ck_refresh_tokens_expiry_after_issue"]);
            Assert.Equal(
                "revoked_reason IS NULL OR (length(revoked_reason) >= 1 AND length(revoked_reason) <= 64)",
                checkConstraints["ck_refresh_tokens_revoked_reason_length"]);
            Assert.Equal(
                "revoked_reason IS NULL OR revoked_reason IN ('logout', 'reuse_detected', 'admin_revoked')",
                checkConstraints["ck_refresh_tokens_revoked_reason_labels"]);
            Assert.Equal(
                "(revoked_at IS NULL AND revoked_reason IS NULL) OR (revoked_at IS NOT NULL AND revoked_reason IS NOT NULL)",
                checkConstraints["ck_refresh_tokens_revocation_pair"]);
        }

        [Fact]
        public void RefreshTokensForeignKeys_MatchTheErd()
        {
            var entity = FindRefreshTokenEntity();

            var adminForeignKey = Assert.Single(entity.GetForeignKeys(), foreignKey =>
                foreignKey.PrincipalEntityType.ClrType == typeof(Infrastructure.Persistence.Master.Identity.PlatformAdminUser));
            Assert.Equal("fk_refresh_tokens_admin_id", adminForeignKey.GetConstraintName());
            Assert.Equal(DeleteBehavior.Restrict, adminForeignKey.DeleteBehavior);
            Assert.Equal(nameof(RefreshToken.AdminId), Assert.Single(adminForeignKey.Properties).Name);

            var selfForeignKey = Assert.Single(entity.GetForeignKeys(), foreignKey =>
                foreignKey.PrincipalEntityType.ClrType == typeof(RefreshToken));
            Assert.Equal("fk_refresh_tokens_replaced_by_id", selfForeignKey.GetConstraintName());
            Assert.Equal(DeleteBehavior.Restrict, selfForeignKey.DeleteBehavior);
            Assert.Equal(nameof(RefreshToken.ReplacedById), Assert.Single(selfForeignKey.Properties).Name);
        }
    }
}
