using System;
using System.Linq;
using CafeHub.Domain.Plans;
using CafeHub.Infrastructure.Persistence.Master;
using CafeHub.Infrastructure.Persistence.Master.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace CafeHub.UnitTests.Plans.Persistence
{
    public sealed class PlansCatalogEfConfigurationTests
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
            var model = CreateModel();
            var entity = model.FindEntityType(typeof(TEntity));
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
        public void PlansCatalog_ContainsExactlyTheThreePlanTables()
        {
            var planEntityTypes = CreateModel()
                .GetEntityTypes()
                .Where(entity =>
                    entity.ClrType == typeof(Plan)
                    || entity.ClrType == typeof(PlanVersion)
                    || entity.ClrType == typeof(PlanEntitlement))
                .ToList();

            Assert.Equal(3, planEntityTypes.Count);
            Assert.Equal(
                new[] { "plan_entitlements", "plan_versions", "plans" },
                planEntityTypes.Select(entity => entity.GetTableName()).OrderBy(name => name));
        }

        [Fact]
        public void PlansTable_MatchesTheErdNameAndPrimaryKey()
        {
            var entity = FindEntity<Plan>();

            Assert.Equal("plans", entity.GetTableName());

            var primaryKey = entity.FindPrimaryKey();
            Assert.NotNull(primaryKey);
            Assert.Equal("pk_plans", primaryKey!.GetName());
            var keyProperty = Assert.Single(primaryKey.Properties);
            Assert.Equal("id", keyProperty.GetColumnName());
            Assert.Equal(typeof(Guid), keyProperty.ClrType);
            Assert.False(keyProperty.IsNullable);
        }

        [Fact]
        public void PlanVersionsTable_MatchesTheErdNameAndPrimaryKey()
        {
            var entity = FindEntity<PlanVersion>();

            Assert.Equal("plan_versions", entity.GetTableName());

            var primaryKey = entity.FindPrimaryKey();
            Assert.NotNull(primaryKey);
            Assert.Equal("pk_plan_versions", primaryKey!.GetName());
            var keyProperty = Assert.Single(primaryKey.Properties);
            Assert.Equal("id", keyProperty.GetColumnName());
            Assert.Equal(typeof(Guid), keyProperty.ClrType);
            Assert.False(keyProperty.IsNullable);
        }

        [Fact]
        public void PlanEntitlementsTable_MatchesTheErdNameAndPrimaryKey()
        {
            var entity = FindEntity<PlanEntitlement>();

            Assert.Equal("plan_entitlements", entity.GetTableName());

            var primaryKey = entity.FindPrimaryKey();
            Assert.NotNull(primaryKey);
            Assert.Equal("pk_plan_entitlements", primaryKey!.GetName());
            var keyProperty = Assert.Single(primaryKey.Properties);
            Assert.Equal("id", keyProperty.GetColumnName());
            Assert.Equal(typeof(Guid), keyProperty.ClrType);
            Assert.False(keyProperty.IsNullable);
        }

        [Fact]
        public void PlansColumns_MatchTheErd()
        {
            var entity = FindEntity<Plan>();

            AssertColumn(entity, nameof(Plan.Id), "id", typeof(Guid), nullable: false);
            AssertColumn(entity, nameof(Plan.Code), "code", typeof(string), nullable: false);
            AssertColumn(entity, nameof(Plan.DisplayName), "display_name", typeof(string), nullable: false);
            var isSelectable = AssertColumn(entity, nameof(Plan.IsSelectable), "is_selectable", typeof(bool), nullable: false);
            AssertColumn(entity, nameof(Plan.CreatedAt), "created_at", typeof(DateTimeOffset), nullable: false);
            AssertColumn(entity, nameof(Plan.UpdatedAt), "updated_at", typeof(DateTimeOffset), nullable: false);
            AssertColumn(entity, nameof(Plan.CreatedBy), "created_by", typeof(Guid?), nullable: true);
            AssertColumn(entity, nameof(Plan.UpdatedBy), "updated_by", typeof(Guid?), nullable: true);

            Assert.Equal(false, isSelectable.GetDefaultValue());
        }

        [Fact]
        public void PlanVersionsColumns_MatchTheErd()
        {
            var entity = FindEntity<PlanVersion>();

            AssertColumn(entity, nameof(PlanVersion.Id), "id", typeof(Guid), nullable: false);
            AssertColumn(entity, nameof(PlanVersion.PlanId), "plan_id", typeof(Guid), nullable: false);
            AssertColumn(entity, nameof(PlanVersion.VersionNumber), "version_number", typeof(int), nullable: false);
            var price = AssertColumn(entity, nameof(PlanVersion.Price), "price", typeof(decimal), nullable: false);
            AssertColumn(entity, nameof(PlanVersion.CurrencyCode), "currency_code", typeof(string), nullable: false);
            var billingPeriod = AssertColumn(entity, nameof(PlanVersion.BillingPeriod), "billing_period", typeof(BillingPeriod), nullable: false);
            var status = AssertColumn(entity, nameof(PlanVersion.Status), "status", typeof(PlanVersionStatus), nullable: false);
            AssertColumn(entity, nameof(PlanVersion.CreatedAt), "created_at", typeof(DateTimeOffset), nullable: false);
            AssertColumn(entity, nameof(PlanVersion.UpdatedAt), "updated_at", typeof(DateTimeOffset), nullable: false);
            AssertColumn(entity, nameof(PlanVersion.CreatedBy), "created_by", typeof(Guid?), nullable: true);
            AssertColumn(entity, nameof(PlanVersion.UpdatedBy), "updated_by", typeof(Guid?), nullable: true);

            Assert.Equal("numeric(12,2)", price.GetColumnType());
            Assert.Equal(12, price.GetPrecision());
            Assert.Equal(2, price.GetScale());
            Assert.Equal("text", billingPeriod.GetColumnType());
            var billingPeriodConverter = billingPeriod.GetTypeMapping().Converter;
            Assert.NotNull(billingPeriodConverter);
            Assert.Equal(typeof(string), billingPeriodConverter!.ProviderClrType);
            Assert.Equal("text", status.GetColumnType());
            var statusConverter = status.GetTypeMapping().Converter;
            Assert.NotNull(statusConverter);
            Assert.Equal(typeof(string), statusConverter!.ProviderClrType);
        }

        [Fact]
        public void PlanEntitlementsColumns_MatchTheErd()
        {
            var entity = FindEntity<PlanEntitlement>();

            AssertColumn(entity, nameof(PlanEntitlement.Id), "id", typeof(Guid), nullable: false);
            AssertColumn(entity, nameof(PlanEntitlement.PlanVersionId), "plan_version_id", typeof(Guid), nullable: false);
            AssertColumn(entity, nameof(PlanEntitlement.Key), "entitlement_key", typeof(string), nullable: false);
            AssertColumn(entity, nameof(PlanEntitlement.Value), "value", typeof(string), nullable: false);
            AssertColumn(entity, nameof(PlanEntitlement.CreatedAt), "created_at", typeof(DateTimeOffset), nullable: false);

            Assert.Null(entity.FindProperty("CreatedBy"));
            Assert.Null(entity.FindProperty("UpdatedAt"));
            Assert.Null(entity.FindProperty("UpdatedBy"));
            Assert.Null(entity.FindProperty("PlanId"));
        }

        [Fact]
        public void PlansCatalogUniqueIndexes_MatchTheErd()
        {
            var plans = FindEntity<Plan>();
            var planVersions = FindEntity<PlanVersion>();
            var planEntitlements = FindEntity<PlanEntitlement>();

            var codeIndex = FindIndex(plans, nameof(Plan.Code));
            Assert.True(codeIndex.IsUnique);
            Assert.Equal("uq_plans_code", codeIndex.GetDatabaseName());

            var versionIndex = FindIndex(planVersions, nameof(PlanVersion.PlanId), nameof(PlanVersion.VersionNumber));
            Assert.True(versionIndex.IsUnique);
            Assert.Equal("uq_plan_versions_plan_version", versionIndex.GetDatabaseName());

            var entitlementIndex = FindIndex(planEntitlements, nameof(PlanEntitlement.PlanVersionId), nameof(PlanEntitlement.Key));
            Assert.True(entitlementIndex.IsUnique);
            Assert.Equal("uq_plan_entitlements_key", entitlementIndex.GetDatabaseName());
        }

        [Fact]
        public void PlansCatalogActorIndexes_MatchTheIndexingStrategy()
        {
            var plans = FindEntity<Plan>();
            var planVersions = FindEntity<PlanVersion>();

            Assert.Equal(3, plans.GetIndexes().Count());
            Assert.Equal(3, planVersions.GetIndexes().Count());
            Assert.Single(FindEntity<PlanEntitlement>().GetIndexes());

            var planCreatedByIndex = FindIndex(plans, nameof(Plan.CreatedBy));
            Assert.False(planCreatedByIndex.IsUnique);
            Assert.Equal("ix_plans_created_by", planCreatedByIndex.GetDatabaseName());

            var planUpdatedByIndex = FindIndex(plans, nameof(Plan.UpdatedBy));
            Assert.False(planUpdatedByIndex.IsUnique);
            Assert.Equal("ix_plans_updated_by", planUpdatedByIndex.GetDatabaseName());

            var versionCreatedByIndex = FindIndex(planVersions, nameof(PlanVersion.CreatedBy));
            Assert.False(versionCreatedByIndex.IsUnique);
            Assert.Equal("ix_plan_versions_created_by", versionCreatedByIndex.GetDatabaseName());

            var versionUpdatedByIndex = FindIndex(planVersions, nameof(PlanVersion.UpdatedBy));
            Assert.False(versionUpdatedByIndex.IsUnique);
            Assert.Equal("ix_plan_versions_updated_by", versionUpdatedByIndex.GetDatabaseName());
        }

        [Fact]
        public void PlansCatalogCheckConstraints_MatchTheErd()
        {
            var plans = FindEntity<Plan>().GetCheckConstraints().ToDictionary(constraint => constraint.Name!, constraint => constraint.Sql);
            Assert.Equal(2, plans.Count);
            Assert.Equal("code ~ '^[a-z][a-z0-9-]{2,63}$'", plans["ck_plans_code_format"]);
            Assert.Equal("length(display_name) >= 1 AND length(display_name) <= 256", plans["ck_plans_display_name_length"]);

            var planVersions = FindEntity<PlanVersion>().GetCheckConstraints().ToDictionary(constraint => constraint.Name!, constraint => constraint.Sql);
            Assert.Equal(5, planVersions.Count);
            Assert.Equal("version_number >= 1", planVersions["ck_plan_versions_version_number_positive"]);
            Assert.Equal("billing_period IN ('Monthly', 'Annual')", planVersions["ck_plan_versions_billing_period"]);
            Assert.Equal("price >= 0", planVersions["ck_plan_versions_price_non_negative"]);
            Assert.Equal("currency_code ~ '^[a-z]{3}$'", planVersions["ck_plan_versions_currency_code_format"]);
            Assert.Equal("status IN ('Draft', 'Active', 'Retired')", planVersions["ck_plan_versions_status"]);

            var planEntitlements = FindEntity<PlanEntitlement>().GetCheckConstraints().ToDictionary(constraint => constraint.Name!, constraint => constraint.Sql);
            Assert.Equal(3, planEntitlements.Count);
            Assert.Equal("entitlement_key ~ '^[a-z][a-z0-9_.]*$'", planEntitlements["ck_plan_entitlements_entitlement_key_format"]);
            Assert.Equal("length(entitlement_key) >= 1 AND length(entitlement_key) <= 128", planEntitlements["ck_plan_entitlements_entitlement_key_length"]);
            Assert.Equal("length(value) >= 1 AND length(value) <= 512", planEntitlements["ck_plan_entitlements_value_length"]);
        }

        [Fact]
        public void PlansCatalogForeignKeys_MatchTheErd()
        {
            var plans = FindEntity<Plan>();
            var planVersions = FindEntity<PlanVersion>();
            var planEntitlements = FindEntity<PlanEntitlement>();

            Assert.Equal(2, plans.GetForeignKeys().Count());
            var planCreatedByForeignKey = plans.GetForeignKeys().Single(foreignKey => foreignKey.GetConstraintName() == "fk_plans_created_by");
            Assert.Equal(typeof(PlatformAdminUser), planCreatedByForeignKey.PrincipalEntityType.ClrType);
            Assert.Equal(nameof(Plan.CreatedBy), Assert.Single(planCreatedByForeignKey.Properties).Name);
            Assert.Equal(DeleteBehavior.Restrict, planCreatedByForeignKey.DeleteBehavior);
            var planUpdatedByForeignKey = plans.GetForeignKeys().Single(foreignKey => foreignKey.GetConstraintName() == "fk_plans_updated_by");
            Assert.Equal(typeof(PlatformAdminUser), planUpdatedByForeignKey.PrincipalEntityType.ClrType);
            Assert.Equal(nameof(Plan.UpdatedBy), Assert.Single(planUpdatedByForeignKey.Properties).Name);
            Assert.Equal(DeleteBehavior.Restrict, planUpdatedByForeignKey.DeleteBehavior);

            Assert.Equal(3, planVersions.GetForeignKeys().Count());
            var versionPlanForeignKey = planVersions.GetForeignKeys().Single(foreignKey => foreignKey.GetConstraintName() == "fk_plan_versions_plan_id");
            Assert.Equal(typeof(Plan), versionPlanForeignKey.PrincipalEntityType.ClrType);
            Assert.Equal(nameof(PlanVersion.PlanId), Assert.Single(versionPlanForeignKey.Properties).Name);
            Assert.True(versionPlanForeignKey.IsRequired);
            Assert.Equal(DeleteBehavior.Restrict, versionPlanForeignKey.DeleteBehavior);
            var versionCreatedByForeignKey = planVersions.GetForeignKeys().Single(foreignKey => foreignKey.GetConstraintName() == "fk_plan_versions_created_by");
            Assert.Equal(typeof(PlatformAdminUser), versionCreatedByForeignKey.PrincipalEntityType.ClrType);
            Assert.Equal(DeleteBehavior.Restrict, versionCreatedByForeignKey.DeleteBehavior);
            var versionUpdatedByForeignKey = planVersions.GetForeignKeys().Single(foreignKey => foreignKey.GetConstraintName() == "fk_plan_versions_updated_by");
            Assert.Equal(typeof(PlatformAdminUser), versionUpdatedByForeignKey.PrincipalEntityType.ClrType);
            Assert.Equal(DeleteBehavior.Restrict, versionUpdatedByForeignKey.DeleteBehavior);

            var entitlementForeignKey = Assert.Single(planEntitlements.GetForeignKeys());
            Assert.Equal("fk_plan_entitlements_plan_version_id", entitlementForeignKey.GetConstraintName());
            Assert.Equal(typeof(PlanVersion), entitlementForeignKey.PrincipalEntityType.ClrType);
            Assert.Equal(nameof(PlanEntitlement.PlanVersionId), Assert.Single(entitlementForeignKey.Properties).Name);
            Assert.True(entitlementForeignKey.IsRequired);
            Assert.Equal(DeleteBehavior.Restrict, entitlementForeignKey.DeleteBehavior);
        }

        [Fact]
        public void PlansCatalogNavigations_MapTheAggregateCollections()
        {
            var plans = FindEntity<Plan>();
            var planVersions = FindEntity<PlanVersion>();

            var versionsNavigation = plans.FindNavigation(nameof(Plan.Versions));
            Assert.NotNull(versionsNavigation);
            Assert.True(versionsNavigation!.IsCollection);
            Assert.Equal(typeof(PlanVersion), versionsNavigation.TargetEntityType.ClrType);

            var entitlementsNavigation = planVersions.FindNavigation(nameof(PlanVersion.Entitlements));
            Assert.NotNull(entitlementsNavigation);
            Assert.True(entitlementsNavigation!.IsCollection);
            Assert.Equal(typeof(PlanEntitlement), entitlementsNavigation.TargetEntityType.ClrType);
        }
    }
}
