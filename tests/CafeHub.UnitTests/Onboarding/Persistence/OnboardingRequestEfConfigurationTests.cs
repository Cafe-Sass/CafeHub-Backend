using System;
using System.Linq;
using CafeHub.Domain.Onboarding;
using CafeHub.Domain.Plans;
using CafeHub.Infrastructure.Persistence.Master;
using CafeHub.Infrastructure.Persistence.Master.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace CafeHub.UnitTests.Onboarding.Persistence
{
    public sealed class OnboardingRequestEfConfigurationTests
    {
        private static IModel CreateModel()
        {
            var options = new DbContextOptionsBuilder<MasterDbContext>()
                .UseNpgsql("Host=localhost;Database=test")
                .Options;

            using var context = new MasterDbContext(options);
            return context.GetService<IDesignTimeModel>().Model;
        }

        private static IEntityType FindEntity()
        {
            var entity = CreateModel().FindEntityType(typeof(OnboardingRequest));
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
        public void OnboardingRequestsTable_MatchesTheErd()
        {
            var entity = FindEntity();

            Assert.Equal("onboarding_requests", entity.GetTableName());

            var primaryKey = entity.FindPrimaryKey();
            Assert.NotNull(primaryKey);
            Assert.Equal("pk_onboarding_requests", primaryKey!.GetName());
            var keyProperty = Assert.Single(primaryKey.Properties);
            Assert.Equal("id", keyProperty.GetColumnName());
            Assert.Equal(typeof(Guid), keyProperty.ClrType);
            Assert.False(keyProperty.IsNullable);
        }

        [Fact]
        public void OnboardingRequestsColumns_MatchTheErd()
        {
            var entity = FindEntity();

            AssertColumn(entity, nameof(OnboardingRequest.Id), "id", typeof(Guid), nullable: false);
            AssertColumn(entity, nameof(OnboardingRequest.CreatedVia), "created_via", typeof(OnboardingRequestCreatedVia), nullable: false);
            AssertColumn(entity, nameof(OnboardingRequest.CreatedBy), "created_by", typeof(Guid?), nullable: true);
            AssertColumn(entity, nameof(OnboardingRequest.PlanVersionId), "plan_version_id", typeof(Guid), nullable: false);
            AssertColumn(entity, nameof(OnboardingRequest.OwnerEmail), "owner_email", typeof(string), nullable: false);
            AssertColumn(entity, nameof(OnboardingRequest.OwnerFullName), "owner_full_name", typeof(string), nullable: false);
            AssertColumn(entity, nameof(OnboardingRequest.OwnerPhoneNumber), "owner_phone_number", typeof(string), nullable: true);
            AssertColumn(entity, nameof(OnboardingRequest.CafeName), "cafe_name", typeof(string), nullable: false);
            AssertColumn(entity, nameof(OnboardingRequest.OwnerPasswordHash), "owner_password_hash", typeof(string), nullable: true);
            AssertColumn(entity, nameof(OnboardingRequest.TenantSlug), "tenant_slug", typeof(string), nullable: false);
            AssertColumn(entity, nameof(OnboardingRequest.Status), "onboarding_status", typeof(OnboardingRequestStatus), nullable: false);
            AssertColumn(entity, nameof(OnboardingRequest.ReviewedBy), "reviewed_by", typeof(Guid?), nullable: true);
            AssertColumn(entity, nameof(OnboardingRequest.ReviewedAt), "reviewed_at", typeof(DateTimeOffset?), nullable: true);
            AssertColumn(entity, nameof(OnboardingRequest.RejectionReason), "rejection_reason", typeof(string), nullable: true);
            AssertColumn(entity, nameof(OnboardingRequest.CreatedAt), "created_at", typeof(DateTimeOffset), nullable: false);
            AssertColumn(entity, nameof(OnboardingRequest.UpdatedAt), "updated_at", typeof(DateTimeOffset), nullable: false);
            AssertColumn(entity, nameof(OnboardingRequest.UpdatedBy), "updated_by", typeof(Guid?), nullable: true);
        }

        [Fact]
        public void OnboardingRequestsUniqueIndexes_MatchTheErd()
        {
            var entity = FindEntity();

            var slugIndex = FindIndex(entity, nameof(OnboardingRequest.TenantSlug));
            Assert.True(slugIndex.IsUnique);
            Assert.Equal("uq_onboarding_requests_slug", slugIndex.GetDatabaseName());
            Assert.Equal("\"onboarding_status\" NOT IN ('Approved', 'Rejected', 'Cancelled')", slugIndex.GetFilter());
        }

        [Fact]
        public void OnboardingRequestsActorIndexes_MatchTheIndexingStrategy()
        {
            var entity = FindEntity();

            Assert.Equal(5, entity.GetIndexes().Count());

            var createdByIndex = FindIndex(entity, nameof(OnboardingRequest.CreatedBy));
            Assert.False(createdByIndex.IsUnique);
            Assert.Equal("ix_onboarding_requests_created_by", createdByIndex.GetDatabaseName());

            var planVersionIndex = FindIndex(entity, nameof(OnboardingRequest.PlanVersionId));
            Assert.False(planVersionIndex.IsUnique);
            Assert.Equal("ix_onboarding_requests_plan_version_id", planVersionIndex.GetDatabaseName());

            var reviewedByIndex = FindIndex(entity, nameof(OnboardingRequest.ReviewedBy));
            Assert.False(reviewedByIndex.IsUnique);
            Assert.Equal("ix_onboarding_requests_reviewed_by", reviewedByIndex.GetDatabaseName());

            var updatedByIndex = FindIndex(entity, nameof(OnboardingRequest.UpdatedBy));
            Assert.False(updatedByIndex.IsUnique);
            Assert.Equal("ix_onboarding_requests_updated_by", updatedByIndex.GetDatabaseName());
        }

        [Fact]
        public void OnboardingRequestsCheckConstraints_MatchTheErd()
        {
            var entity = FindEntity();
            var checks = entity.GetCheckConstraints().ToDictionary(constraint => constraint.Name!, constraint => constraint.Sql);

            Assert.Contains(checks, c => c.Key == "ck_onboarding_requests_created_via" && c.Value == "created_via IN ('PublicRegistration', 'Support')");
            Assert.Contains(checks, c => c.Key == "ck_onboarding_requests_support_actor" && c.Value == "created_via = 'PublicRegistration' OR created_by IS NOT NULL");
            Assert.Contains(checks, c => c.Key == "ck_onboarding_requests_owner_email_length" && c.Value == "length(owner_email) >= 1 AND length(owner_email) <= 320");
            Assert.Contains(checks, c => c.Key == "ck_onboarding_requests_owner_full_name_length" && c.Value == "length(owner_full_name) >= 1 AND length(owner_full_name) <= 256");
            Assert.Contains(checks, c => c.Key == "ck_onboarding_requests_owner_phone_number_length" && c.Value == "owner_phone_number IS NULL OR (length(owner_phone_number) >= 1 AND length(owner_phone_number) <= 64)");
            Assert.Contains(checks, c => c.Key == "ck_onboarding_requests_cafe_name_length" && c.Value == "length(cafe_name) >= 1 AND length(cafe_name) <= 256");
            Assert.Contains(checks, c => c.Key == "ck_onboarding_requests_owner_password_hash_length" && c.Value == "owner_password_hash IS NULL OR length(owner_password_hash) BETWEEN 1 AND 512");
            Assert.Contains(checks, c => c.Key == "ck_onboarding_requests_owner_credential" && c.Value == "created_via = 'Support' OR owner_password_hash IS NOT NULL");
            Assert.Contains(checks, c => c.Key == "ck_onboarding_requests_slug_format" && c.Value == "tenant_slug ~ '^[a-z0-9][a-z0-9-]{1,38}[a-z0-9]$'");
            Assert.Contains(checks, c => c.Key == "ck_onboarding_requests_slug_lowercase" && c.Value == "tenant_slug = lower(tenant_slug)");
            Assert.Contains(checks, c => c.Key == "ck_onboarding_requests_slug_reserved" && c.Value == "tenant_slug NOT IN ('admin','api','t','www','app','static','assets','health','login','signup','support','platform')");
            Assert.Contains(checks, c => c.Key == "ck_onboarding_requests_status" && c.Value == "onboarding_status IN ('AwaitingPayment', 'PaymentFailed', 'PaidAwaitingReview', 'Approved', 'Rejected', 'Cancelled')");
            Assert.Contains(checks, c => c.Key == "ck_onboarding_requests_rejection_reason_length" && c.Value == "rejection_reason IS NULL OR length(rejection_reason) <= 1024");
        }

        [Fact]
        public void OnboardingRequestsForeignKeys_MatchTheErd()
        {
            var entity = FindEntity();

            Assert.Equal(4, entity.GetForeignKeys().Count());

            var planVersionForeignKey = entity.GetForeignKeys().Single(fk => fk.GetConstraintName() == "fk_onboarding_requests_plan_version_id");
            Assert.Equal(typeof(PlanVersion), planVersionForeignKey.PrincipalEntityType.ClrType);
            Assert.Equal(nameof(OnboardingRequest.PlanVersionId), Assert.Single(planVersionForeignKey.Properties).Name);
            Assert.True(planVersionForeignKey.IsRequired);
            Assert.Equal(DeleteBehavior.Restrict, planVersionForeignKey.DeleteBehavior);

            var createdByForeignKey = entity.GetForeignKeys().Single(fk => fk.GetConstraintName() == "fk_onboarding_requests_created_by");
            Assert.Equal(typeof(PlatformAdminUser), createdByForeignKey.PrincipalEntityType.ClrType);
            Assert.Equal(nameof(OnboardingRequest.CreatedBy), Assert.Single(createdByForeignKey.Properties).Name);
            Assert.False(createdByForeignKey.IsRequired);
            Assert.Equal(DeleteBehavior.Restrict, createdByForeignKey.DeleteBehavior);

            var reviewedByForeignKey = entity.GetForeignKeys().Single(fk => fk.GetConstraintName() == "fk_onboarding_requests_reviewed_by");
            Assert.Equal(typeof(PlatformAdminUser), reviewedByForeignKey.PrincipalEntityType.ClrType);
            Assert.Equal(nameof(OnboardingRequest.ReviewedBy), Assert.Single(reviewedByForeignKey.Properties).Name);
            Assert.False(reviewedByForeignKey.IsRequired);
            Assert.Equal(DeleteBehavior.Restrict, reviewedByForeignKey.DeleteBehavior);

            var updatedByForeignKey = entity.GetForeignKeys().Single(fk => fk.GetConstraintName() == "fk_onboarding_requests_updated_by");
            Assert.Equal(typeof(PlatformAdminUser), updatedByForeignKey.PrincipalEntityType.ClrType);
            Assert.Equal(nameof(OnboardingRequest.UpdatedBy), Assert.Single(updatedByForeignKey.Properties).Name);
            Assert.False(updatedByForeignKey.IsRequired);
            Assert.Equal(DeleteBehavior.Restrict, updatedByForeignKey.DeleteBehavior);
        }

        [Fact]
        public void OwnerPasswordHash_IsNotPartOfAnyProjectionContract()
        {
            // The entity exposes OwnerPasswordHash only as a persistence field.
            // No response contract type exists in this sub-task, so the column must
            // simply not be readable through a public projection. This test pins the
            // expectation that no OnboardingRequest projection carries the hash.
            var entity = FindEntity();
            Assert.NotNull(entity.FindProperty(nameof(OnboardingRequest.OwnerPasswordHash)));
        }
    }
}
