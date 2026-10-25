using System;
using System.Linq;
using CafeHub.Domain.Onboarding;
using CafeHub.Domain.Plans;
using CafeHub.Domain.Subscriptions;
using CafeHub.Infrastructure.Persistence.Master;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace CafeHub.UnitTests.Subscriptions.Persistence
{
    public sealed class SubscriptionsAndPaymentsEfConfigurationTests
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
        public void SubscriptionsTable_MatchesTheErd()
        {
            var entity = FindEntity<Subscription>();

            Assert.Equal("subscriptions", entity.GetTableName());

            var primaryKey = entity.FindPrimaryKey();
            Assert.NotNull(primaryKey);
            Assert.Equal("pk_subscriptions", primaryKey!.GetName());
            var keyProperty = Assert.Single(primaryKey.Properties);
            Assert.Equal("id", keyProperty.GetColumnName());
            Assert.Equal(typeof(Guid), keyProperty.ClrType);
            Assert.False(keyProperty.IsNullable);
        }

        [Fact]
        public void SubscriptionsColumns_MatchTheErd()
        {
            var entity = FindEntity<Subscription>();

            AssertColumn(entity, nameof(Subscription.Id), "id", typeof(Guid), nullable: false);
            AssertColumn(entity, nameof(Subscription.OnboardingRequestId), "onboarding_request_id", typeof(Guid?), nullable: true);
            AssertColumn(entity, nameof(Subscription.TenantId), "tenant_id", typeof(Guid?), nullable: true);
            AssertColumn(entity, nameof(Subscription.Status), "status", typeof(SubscriptionStatus), nullable: false);
            AssertColumn(entity, nameof(Subscription.CreatedAt), "created_at", typeof(DateTimeOffset), nullable: false);
            AssertColumn(entity, nameof(Subscription.UpdatedAt), "updated_at", typeof(DateTimeOffset), nullable: false);
            AssertColumn(entity, nameof(Subscription.CreatedBy), "created_by", typeof(Guid?), nullable: true);
            AssertColumn(entity, nameof(Subscription.UpdatedBy), "updated_by", typeof(Guid?), nullable: true);

            Assert.Null(entity.FindProperty("PlanVersionId"));
        }

        [Fact]
        public void SubscriptionsUniqueIndexes_MatchTheErd()
        {
            var entity = FindEntity<Subscription>();

            var requestIndex = FindIndex(entity, nameof(Subscription.OnboardingRequestId));
            Assert.True(requestIndex.IsUnique);
            Assert.Equal("uq_subscriptions_onboarding_request_id", requestIndex.GetDatabaseName());
            Assert.Equal("\"onboarding_request_id\" IS NOT NULL", requestIndex.GetFilter());

            var tenantIndex = FindIndex(entity, nameof(Subscription.TenantId));
            Assert.True(tenantIndex.IsUnique);
            Assert.Equal("uq_subscriptions_tenant_id", tenantIndex.GetDatabaseName());
            Assert.Equal("\"tenant_id\" IS NOT NULL", tenantIndex.GetFilter());
        }

        [Fact]
        public void SubscriptionsCheckConstraints_MatchTheErd()
        {
            var entity = FindEntity<Subscription>();
            var checks = entity.GetCheckConstraints().ToDictionary(c => c.Name!, c => c.Sql);

            Assert.Contains(checks, c => c.Key == "ck_subscriptions_status" && c.Value == "status IN ('Active', 'Suspended', 'Expired', 'Cancelled')");
            Assert.Contains(checks, c => c.Key == "ck_subscriptions_owner" && c.Value == "num_nonnulls(onboarding_request_id, tenant_id) = 1");
        }

        [Fact]
        public void SubscriptionsForeignKeys_MatchTheErd()
        {
            var entity = FindEntity<Subscription>();

            var requestFk = Assert.Single(entity.GetForeignKeys());
            Assert.Equal("fk_subscriptions_onboarding_request_id", requestFk.GetConstraintName());
            Assert.Equal(typeof(OnboardingRequest), requestFk.PrincipalEntityType.ClrType);
            Assert.False(requestFk.IsRequired);
            Assert.Equal(DeleteBehavior.Restrict, requestFk.DeleteBehavior);
        }

        [Fact]
        public void SubscriptionPeriodsTable_MatchesTheErd()
        {
            var entity = FindEntity<SubscriptionPeriod>();

            Assert.Equal("subscription_periods", entity.GetTableName());

            var primaryKey = entity.FindPrimaryKey();
            Assert.NotNull(primaryKey);
            Assert.Equal("pk_subscription_periods", primaryKey!.GetName());
            var keyProperty = Assert.Single(primaryKey.Properties);
            Assert.Equal("id", keyProperty.GetColumnName());
            Assert.Equal(typeof(Guid), keyProperty.ClrType);
            Assert.False(keyProperty.IsNullable);
        }

        [Fact]
        public void SubscriptionPeriodsColumns_MatchTheErd()
        {
            var entity = FindEntity<SubscriptionPeriod>();

            AssertColumn(entity, nameof(SubscriptionPeriod.Id), "id", typeof(Guid), nullable: false);
            AssertColumn(entity, nameof(SubscriptionPeriod.SubscriptionId), "subscription_id", typeof(Guid), nullable: false);
            AssertColumn(entity, nameof(SubscriptionPeriod.PlanVersionId), "plan_version_id", typeof(Guid), nullable: false);
            AssertColumn(entity, nameof(SubscriptionPeriod.PeriodNumber), "period_number", typeof(int), nullable: false);
            AssertColumn(entity, nameof(SubscriptionPeriod.PeriodStatus), "period_status", typeof(SubscriptionPeriodStatus), nullable: false);
            AssertColumn(entity, nameof(SubscriptionPeriod.AgreedPrice), "agreed_price", typeof(decimal), nullable: false);
            AssertColumn(entity, nameof(SubscriptionPeriod.CurrencyCode), "currency_code", typeof(string), nullable: false);
            AssertColumn(entity, nameof(SubscriptionPeriod.StartsAt), "starts_at", typeof(DateTimeOffset), nullable: false);
            AssertColumn(entity, nameof(SubscriptionPeriod.EndsAt), "ends_at", typeof(DateTimeOffset?), nullable: true);
            AssertColumn(entity, nameof(SubscriptionPeriod.RenewalRequestId), "renewal_request_id", typeof(Guid?), nullable: true);
            AssertColumn(entity, nameof(SubscriptionPeriod.CreatedAt), "created_at", typeof(DateTimeOffset), nullable: false);
            AssertColumn(entity, nameof(SubscriptionPeriod.UpdatedAt), "updated_at", typeof(DateTimeOffset), nullable: false);
            AssertColumn(entity, nameof(SubscriptionPeriod.CreatedBy), "created_by", typeof(Guid?), nullable: true);
            AssertColumn(entity, nameof(SubscriptionPeriod.UpdatedBy), "updated_by", typeof(Guid?), nullable: true);
        }

        [Fact]
        public void SubscriptionPeriodsUniqueIndexes_MatchTheErd()
        {
            var entity = FindEntity<SubscriptionPeriod>();

            var periodNumberIndex = FindIndex(entity, nameof(SubscriptionPeriod.SubscriptionId), nameof(SubscriptionPeriod.PeriodNumber));
            Assert.True(periodNumberIndex.IsUnique);
            Assert.Equal("uq_subscription_periods_period_number", periodNumberIndex.GetDatabaseName());

            var activeIndex = FindIndex(entity, nameof(SubscriptionPeriod.SubscriptionId));
            Assert.True(activeIndex.IsUnique);
            Assert.Equal("uq_subscription_periods_active", activeIndex.GetDatabaseName());
            Assert.Equal("\"period_status\" = 'Active'", activeIndex.GetFilter());

            var renewalIndex = FindIndex(entity, nameof(SubscriptionPeriod.RenewalRequestId));
            Assert.True(renewalIndex.IsUnique);
            Assert.Equal("uq_subscription_periods_renewal_request", renewalIndex.GetDatabaseName());
            Assert.Equal("\"renewal_request_id\" IS NOT NULL", renewalIndex.GetFilter());
        }

        [Fact]
        public void SubscriptionPeriodsCheckConstraints_MatchTheErd()
        {
            var entity = FindEntity<SubscriptionPeriod>();
            var checks = entity.GetCheckConstraints().ToDictionary(c => c.Name!, c => c.Sql);

            Assert.Contains(checks, c => c.Key == "ck_subscription_periods_period_number_positive" && c.Value == "period_number >= 1");
            Assert.Contains(checks, c => c.Key == "ck_subscription_periods_status" && c.Value == "period_status IN ('Active', 'Ended', 'Cancelled')");
            Assert.Contains(checks, c => c.Key == "ck_subscription_periods_price_non_negative" && c.Value == "agreed_price >= 0");
            Assert.Contains(checks, c => c.Key == "ck_subscription_periods_currency_code_format" && c.Value == "currency_code ~ '^[a-z]{3}$'");
        }

        [Fact]
        public void SubscriptionPeriodsForeignKeys_MatchTheErd()
        {
            var entity = FindEntity<SubscriptionPeriod>();

            Assert.Equal(2, entity.GetForeignKeys().Count());
            var subscriptionFk = entity.GetForeignKeys().Single(fk => fk.GetConstraintName() == "fk_subscription_periods_subscription_id");
            Assert.Equal(typeof(Subscription), subscriptionFk.PrincipalEntityType.ClrType);
            Assert.True(subscriptionFk.IsRequired);
            Assert.Equal(DeleteBehavior.Restrict, subscriptionFk.DeleteBehavior);

            var planVersionFk = entity.GetForeignKeys().Single(fk => fk.GetConstraintName() == "fk_subscription_periods_plan_version_id");
            Assert.Equal(typeof(PlanVersion), planVersionFk.PrincipalEntityType.ClrType);
            Assert.True(planVersionFk.IsRequired);
            Assert.Equal(DeleteBehavior.Restrict, planVersionFk.DeleteBehavior);
        }

        [Fact]
        public void PlatformPaymentTransactionsTable_MatchesTheErd()
        {
            var entity = FindEntity<PlatformPaymentTransaction>();

            Assert.Equal("platform_payment_transactions", entity.GetTableName());

            var primaryKey = entity.FindPrimaryKey();
            Assert.NotNull(primaryKey);
            Assert.Equal("pk_platform_payment_transactions", primaryKey!.GetName());
            var keyProperty = Assert.Single(primaryKey.Properties);
            Assert.Equal("id", keyProperty.GetColumnName());
            Assert.Equal(typeof(Guid), keyProperty.ClrType);
            Assert.False(keyProperty.IsNullable);
        }

        [Fact]
        public void PlatformPaymentTransactionsColumns_MatchTheErd()
        {
            var entity = FindEntity<PlatformPaymentTransaction>();

            AssertColumn(entity, nameof(PlatformPaymentTransaction.Id), "id", typeof(Guid), nullable: false);
            AssertColumn(entity, nameof(PlatformPaymentTransaction.OnboardingRequestId), "onboarding_request_id", typeof(Guid?), nullable: true);
            AssertColumn(entity, nameof(PlatformPaymentTransaction.RenewalRequestId), "renewal_request_id", typeof(Guid?), nullable: true);
            AssertColumn(entity, nameof(PlatformPaymentTransaction.IdempotencyKey), "idempotency_key", typeof(string), nullable: false);
            AssertColumn(entity, nameof(PlatformPaymentTransaction.Amount), "amount", typeof(decimal), nullable: false);
            AssertColumn(entity, nameof(PlatformPaymentTransaction.CurrencyCode), "currency_code", typeof(string), nullable: false);
            AssertColumn(entity, nameof(PlatformPaymentTransaction.Status), "status", typeof(PlatformPaymentTransactionStatus), nullable: false);
            AssertColumn(entity, nameof(PlatformPaymentTransaction.SettledAt), "settled_at", typeof(DateTimeOffset?), nullable: true);
            AssertColumn(entity, nameof(PlatformPaymentTransaction.FailureCode), "failure_code", typeof(string), nullable: true);
            AssertColumn(entity, nameof(PlatformPaymentTransaction.CreatedAt), "created_at", typeof(DateTimeOffset), nullable: false);
            AssertColumn(entity, nameof(PlatformPaymentTransaction.UpdatedAt), "updated_at", typeof(DateTimeOffset), nullable: false);
            AssertColumn(entity, nameof(PlatformPaymentTransaction.CreatedBy), "created_by", typeof(Guid?), nullable: true);
            AssertColumn(entity, nameof(PlatformPaymentTransaction.UpdatedBy), "updated_by", typeof(Guid?), nullable: true);
        }

        [Fact]
        public void PlatformPaymentTransactionsUniqueIdempotencyIndex_MatchTheErd()
        {
            var entity = FindEntity<PlatformPaymentTransaction>();

            var keyIndex = FindIndex(entity, nameof(PlatformPaymentTransaction.IdempotencyKey));
            Assert.True(keyIndex.IsUnique);
            Assert.Equal("uq_platform_payment_transactions_key", keyIndex.GetDatabaseName());
        }

        [Fact]
        public void PlatformPaymentTransactionsCheckConstraints_MatchTheErd()
        {
            var entity = FindEntity<PlatformPaymentTransaction>();
            var checks = entity.GetCheckConstraints().ToDictionary(c => c.Name!, c => c.Sql);

            Assert.Contains(checks, c => c.Key == "ck_platform_payment_transactions_target" && c.Value == "num_nonnulls(onboarding_request_id, renewal_request_id) = 1");
            Assert.Contains(checks, c => c.Key == "ck_platform_payment_transactions_status" && c.Value == "status IN ('Initiated', 'Succeeded', 'Failed')");
            Assert.Contains(checks, c => c.Key == "ck_platform_payment_transactions_idempotency_key_length" && c.Value == "length(idempotency_key) >= 8 AND length(idempotency_key) <= 128");
            Assert.Contains(checks, c => c.Key == "ck_platform_payment_transactions_amount_non_negative" && c.Value == "amount >= 0");
            Assert.Contains(checks, c => c.Key == "ck_platform_payment_transactions_currency_code_format" && c.Value == "currency_code ~ '^[a-z]{3}$'");
            Assert.Contains(checks, c => c.Key == "ck_platform_payment_transactions_failure_code_length" && c.Value == "failure_code IS NULL OR length(failure_code) <= 128");
        }

        [Fact]
        public void PlatformPaymentTransactionsForeignKeys_MatchTheErd()
        {
            var entity = FindEntity<PlatformPaymentTransaction>();

            var requestFk = Assert.Single(entity.GetForeignKeys());
            Assert.Equal("fk_platform_payment_transactions_onboarding_request_id", requestFk.GetConstraintName());
            Assert.Equal(typeof(OnboardingRequest), requestFk.PrincipalEntityType.ClrType);
            Assert.False(requestFk.IsRequired);
            Assert.Equal(DeleteBehavior.Restrict, requestFk.DeleteBehavior);
        }

        [Fact]
        public void PaymentModel_HasNoCardRelatedColumns()
        {
            var entity = FindEntity<PlatformPaymentTransaction>();
            var columnNames = entity.GetProperties().Select(p => p.GetColumnName()).ToHashSet();

            Assert.DoesNotContain("card_number", columnNames);
            Assert.DoesNotContain("pan", columnNames);
            Assert.DoesNotContain("cvv", columnNames);
            Assert.DoesNotContain("expiry", columnNames);
            Assert.DoesNotContain("token", columnNames);
        }
    }
}
