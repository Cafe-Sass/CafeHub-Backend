using System;
using System.Linq;
using CafeHub.Domain.Provisioning;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Master;
using CafeHub.Infrastructure.Persistence.Master.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace CafeHub.UnitTests.Provisioning.Persistence
{
    public sealed class ProvisioningJobEfConfigurationTests
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
        public void ProvisioningJobsTable_MatchesTheErd()
        {
            var entity = FindEntity<ProvisioningJob>();

            Assert.Equal("provisioning_jobs", entity.GetTableName());

            var primaryKey = entity.FindPrimaryKey();
            Assert.NotNull(primaryKey);
            Assert.Equal("pk_provisioning_jobs", primaryKey!.GetName());
            var keyProperty = Assert.Single(primaryKey.Properties);
            Assert.Equal("id", keyProperty.GetColumnName());
            Assert.Equal(typeof(Guid), keyProperty.ClrType);
            Assert.False(keyProperty.IsNullable);
        }

        [Fact]
        public void ProvisioningJobsColumns_MatchTheErd()
        {
            var entity = FindEntity<ProvisioningJob>();

            AssertColumn(entity, nameof(ProvisioningJob.Id), "id", typeof(Guid), nullable: false);
            AssertColumn(entity, nameof(ProvisioningJob.TenantId), "tenant_id", typeof(Guid), nullable: false);
            AssertColumn(entity, nameof(ProvisioningJob.Status), "status", typeof(ProvisioningJobStatus), nullable: false);
            AssertColumn(entity, nameof(ProvisioningJob.CreatedAt), "created_at", typeof(DateTimeOffset), nullable: false);
            AssertColumn(entity, nameof(ProvisioningJob.UpdatedAt), "updated_at", typeof(DateTimeOffset), nullable: false);
            AssertColumn(entity, nameof(ProvisioningJob.CreatedBy), "created_by", typeof(Guid?), nullable: true);
            AssertColumn(entity, nameof(ProvisioningJob.UpdatedBy), "updated_by", typeof(Guid?), nullable: true);
        }

        [Fact]
        public void ProvisioningJobsUniqueIndex_MatchTheErd()
        {
            var entity = FindEntity<ProvisioningJob>();

            var tenantIndex = FindIndex(entity, nameof(ProvisioningJob.TenantId));
            Assert.True(tenantIndex.IsUnique);
            Assert.Equal("uq_provisioning_jobs_tenant_id", tenantIndex.GetDatabaseName());
            Assert.Null(tenantIndex.GetFilter());
        }

        [Fact]
        public void ProvisioningJobsCheckConstraints_MatchTheErd()
        {
            var entity = FindEntity<ProvisioningJob>();
            var checks = entity.GetCheckConstraints().ToDictionary(c => c.Name!, c => c.Sql);

            Assert.Contains(checks, c => c.Key == "ck_provisioning_jobs_status" && c.Value == "status IN ('Pending', 'Running', 'Succeeded', 'Failed')");
        }

        [Fact]
        public void ProvisioningJobsForeignKeys_MatchTheErd()
        {
            var entity = FindEntity<ProvisioningJob>();

            Assert.Equal(3, entity.GetForeignKeys().Count());
            var tenantFk = entity.GetForeignKeys().Single(fk => fk.GetConstraintName() == "fk_provisioning_jobs_tenant_id");
            Assert.Equal(typeof(Tenant), tenantFk.PrincipalEntityType.ClrType);
            Assert.True(tenantFk.IsRequired);
            Assert.Equal(DeleteBehavior.Restrict, tenantFk.DeleteBehavior);

            var createdByFk = entity.GetForeignKeys().Single(fk => fk.GetConstraintName() == "fk_provisioning_jobs_created_by");
            Assert.Equal(typeof(PlatformAdminUser), createdByFk.PrincipalEntityType.ClrType);
            Assert.False(createdByFk.IsRequired);
            Assert.Equal(DeleteBehavior.Restrict, createdByFk.DeleteBehavior);

            var updatedByFk = entity.GetForeignKeys().Single(fk => fk.GetConstraintName() == "fk_provisioning_jobs_updated_by");
            Assert.Equal(typeof(PlatformAdminUser), updatedByFk.PrincipalEntityType.ClrType);
            Assert.False(updatedByFk.IsRequired);
            Assert.Equal(DeleteBehavior.Restrict, updatedByFk.DeleteBehavior);
        }

        [Fact]
        public void ProvisioningJobAttemptsTable_MatchesTheErd()
        {
            var entity = FindEntity<ProvisioningJobAttempt>();

            Assert.Equal("provisioning_job_attempts", entity.GetTableName());

            var primaryKey = entity.FindPrimaryKey();
            Assert.NotNull(primaryKey);
            Assert.Equal("pk_provisioning_job_attempts", primaryKey!.GetName());
            var keyProperty = Assert.Single(primaryKey.Properties);
            Assert.Equal("id", keyProperty.GetColumnName());
            Assert.Equal(typeof(Guid), keyProperty.ClrType);
            Assert.False(keyProperty.IsNullable);
        }

        [Fact]
        public void ProvisioningJobAttemptsColumns_MatchTheErd()
        {
            var entity = FindEntity<ProvisioningJobAttempt>();

            AssertColumn(entity, nameof(ProvisioningJobAttempt.Id), "id", typeof(Guid), nullable: false);
            AssertColumn(entity, nameof(ProvisioningJobAttempt.ProvisioningJobId), "provisioning_job_id", typeof(Guid), nullable: false);
            AssertColumn(entity, nameof(ProvisioningJobAttempt.AttemptNumber), "attempt_number", typeof(int), nullable: false);
            AssertColumn(entity, nameof(ProvisioningJobAttempt.Status), "status", typeof(ProvisioningJobAttemptStatus), nullable: false);
            AssertColumn(entity, nameof(ProvisioningJobAttempt.StartedAt), "started_at", typeof(DateTimeOffset), nullable: false);
            AssertColumn(entity, nameof(ProvisioningJobAttempt.FinishedAt), "finished_at", typeof(DateTimeOffset?), nullable: true);
            AssertColumn(entity, nameof(ProvisioningJobAttempt.ErrorCode), "error_code", typeof(string), nullable: true);
            AssertColumn(entity, nameof(ProvisioningJobAttempt.CreatedAt), "created_at", typeof(DateTimeOffset), nullable: false);
            AssertColumn(entity, nameof(ProvisioningJobAttempt.UpdatedAt), "updated_at", typeof(DateTimeOffset), nullable: false);
        }

        [Fact]
        public void ProvisioningJobAttemptsUniqueIndex_MatchTheErd()
        {
            var entity = FindEntity<ProvisioningJobAttempt>();

            var attemptIndex = FindIndex(entity, nameof(ProvisioningJobAttempt.ProvisioningJobId), nameof(ProvisioningJobAttempt.AttemptNumber));
            Assert.True(attemptIndex.IsUnique);
            Assert.Equal("uq_provisioning_job_attempts_attempt", attemptIndex.GetDatabaseName());
            Assert.Null(attemptIndex.GetFilter());
        }

        [Fact]
        public void ProvisioningJobAttemptsCheckConstraints_MatchTheErd()
        {
            var entity = FindEntity<ProvisioningJobAttempt>();
            var checks = entity.GetCheckConstraints().ToDictionary(c => c.Name!, c => c.Sql);

            Assert.Contains(checks, c => c.Key == "ck_provisioning_job_attempts_attempt_number_positive" && c.Value == "attempt_number >= 1");
            Assert.Contains(checks, c => c.Key == "ck_provisioning_job_attempts_status" && c.Value == "status IN ('Running', 'Succeeded', 'Failed')");
            Assert.Contains(checks, c => c.Key == "ck_provisioning_job_attempts_error_code_length" && c.Value == "error_code IS NULL OR length(error_code) <= 128");
        }

        [Fact]
        public void ProvisioningJobAttemptsForeignKeys_MatchTheErd()
        {
            var entity = FindEntity<ProvisioningJobAttempt>();

            Assert.Single(entity.GetForeignKeys());
            var jobFk = Assert.Single(entity.GetForeignKeys());
            Assert.Equal("fk_provisioning_job_attempts_provisioning_job_id", jobFk.GetConstraintName());
            Assert.Equal(typeof(ProvisioningJob), jobFk.PrincipalEntityType.ClrType);
            Assert.True(jobFk.IsRequired);
            Assert.Equal(DeleteBehavior.Restrict, jobFk.DeleteBehavior);
        }

        [Fact]
        public void ProvisioningJobModel_HasNoSecretColumns()
        {
            var entity = FindEntity<ProvisioningJob>();
            var columnNames = entity.GetProperties().Select(p => p.GetColumnName()).ToHashSet();

            Assert.DoesNotContain("connection_string", columnNames);
            Assert.DoesNotContain("connectionstring", columnNames);
            Assert.DoesNotContain("password", columnNames);
            Assert.DoesNotContain("pwd", columnNames);
            Assert.DoesNotContain("secret", columnNames);
            Assert.DoesNotContain("token", columnNames);
        }

        [Fact]
        public void ProvisioningJobAttemptModel_HasNoSecretColumns()
        {
            var entity = FindEntity<ProvisioningJobAttempt>();
            var columnNames = entity.GetProperties().Select(p => p.GetColumnName()).ToHashSet();

            Assert.DoesNotContain("connection_string", columnNames);
            Assert.DoesNotContain("connectionstring", columnNames);
            Assert.DoesNotContain("password", columnNames);
            Assert.DoesNotContain("pwd", columnNames);
            Assert.DoesNotContain("secret", columnNames);
            Assert.DoesNotContain("token", columnNames);
        }
    }
}
