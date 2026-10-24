using System;
using System.Linq;
using CafeHub.Infrastructure.Persistence.Master;
using CafeHub.Infrastructure.Persistence.Master.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace CafeHub.UnitTests
{
    public sealed class PlatformAuditRecordEfConfigurationTests
    {
        private static IModel CreateModel()
        {
            var options = new DbContextOptionsBuilder<MasterDbContext>()
                .UseNpgsql("Host=localhost;Database=test")
                .Options;

            using var context = new MasterDbContext(options);
            return context.GetService<IDesignTimeModel>().Model;
        }

        private static IEntityType FindAuditRecordEntity()
        {
            var model = CreateModel();
            var entity = model.FindEntityType(typeof(PlatformAuditRecord));
            Assert.NotNull(entity);
            return entity!;
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
        public void PlatformAuditRecordsTable_MatchesTheErdName_AndPrimaryKey()
        {
            var entity = FindAuditRecordEntity();

            Assert.Equal("platform_audit_records", entity.GetTableName());

            var primaryKey = entity.FindPrimaryKey();
            Assert.NotNull(primaryKey);
            Assert.Equal("pk_platform_audit_records", primaryKey.GetName());
            var keyProperty = Assert.Single(primaryKey.Properties);
            Assert.Equal("id", keyProperty.GetColumnName());
            Assert.Equal(typeof(Guid), keyProperty.ClrType);
        }

        [Fact]
        public void PlatformAuditRecordsColumns_MatchTheErd()
        {
            var entity = FindAuditRecordEntity();

            AssertColumn(entity, nameof(PlatformAuditRecord.RecordedAt), "recorded_at", typeof(DateTimeOffset), nullable: false);
            AssertColumn(entity, nameof(PlatformAuditRecord.ActorPlatformAdminId), "actor_platform_admin_id", typeof(Guid?), nullable: true);
            AssertColumn(entity, nameof(PlatformAuditRecord.ActorKind), "actor_kind", typeof(string), nullable: false);
            AssertColumn(entity, nameof(PlatformAuditRecord.ActionCode), "action_code", typeof(string), nullable: false);
            AssertColumn(entity, nameof(PlatformAuditRecord.SubjectType), "subject_type", typeof(string), nullable: false);
            AssertColumn(entity, nameof(PlatformAuditRecord.SubjectId), "subject_id", typeof(Guid?), nullable: true);
            AssertColumn(entity, nameof(PlatformAuditRecord.CorrelationId), "correlation_id", typeof(string), nullable: true);
            AssertColumn(entity, nameof(PlatformAuditRecord.ReasonCode), "reason_code", typeof(string), nullable: true);
            AssertColumn(entity, nameof(PlatformAuditRecord.BeforeState), "before_state", typeof(string), nullable: true);
            AssertColumn(entity, nameof(PlatformAuditRecord.AfterState), "after_state", typeof(string), nullable: true);
        }

        [Fact]
        public void PlatformAuditRecordsCheckConstraints_MatchTheErd()
        {
            var entity = FindAuditRecordEntity();

            var checkConstraints = entity.GetCheckConstraints()
                .ToDictionary(constraint => constraint.Name, constraint => constraint.Sql);

            Assert.Equal(5, checkConstraints.Count);
            Assert.Equal("actor_kind IN ('PlatformAdmin', 'System')", checkConstraints["ck_platform_audit_records_kind"]);
            Assert.Equal("length(action_code) >= 1 AND length(action_code) <= 128", checkConstraints["ck_platform_audit_records_action_code_length"]);
            Assert.Equal("length(subject_type) >= 1 AND length(subject_type) <= 64", checkConstraints["ck_platform_audit_records_subject_type_length"]);
            Assert.Equal("correlation_id IS NULL OR correlation_id ~ '^[A-Za-z0-9._:-]{1,64}$'", checkConstraints["ck_platform_audit_records_correlation_id_format"]);
            Assert.Equal("reason_code IS NULL OR reason_code ~ '^[a-z][a-z0-9_]{0,63}$'", checkConstraints["ck_platform_audit_records_reason_code_format"]);
        }

        [Fact]
        public void PlatformAuditRecordsForeignKey_MatchesTheErd_AndIsIndexedPerR1()
        {
            var entity = FindAuditRecordEntity();

            var foreignKey = Assert.Single(entity.GetForeignKeys());
            Assert.Equal("fk_platform_audit_records_actor_platform_admin_id", foreignKey.GetConstraintName());
            Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
            Assert.Equal(nameof(PlatformAuditRecord.ActorPlatformAdminId), Assert.Single(foreignKey.Properties).Name);

            var actorIndex = entity.GetIndexes().Single(i => i.Properties.Any(p => p.Name == nameof(PlatformAuditRecord.ActorPlatformAdminId)));
            Assert.False(actorIndex.IsUnique);
            Assert.Equal("ix_platform_audit_records_actor_platform_admin_id", actorIndex.GetDatabaseName());
        }
    }
}
