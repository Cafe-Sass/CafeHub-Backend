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

            var foreignKeys = entity.GetForeignKeys()
                .ToDictionary(fk => fk.GetConstraintName()!);
            Assert.Equal(2, foreignKeys.Count);
            var requestFk = foreignKeys["fk_platform_payment_transactions_onboarding_request_id"];
            Assert.Equal(typeof(OnboardingRequest), requestFk.PrincipalEntityType.ClrType);
            Assert.False(requestFk.IsRequired);
            Assert.Equal(DeleteBehavior.Restrict, requestFk.DeleteBehavior);
            var renewalFk = foreignKeys["fk_platform_payment_transactions_renewal_request_id"];
            Assert.Equal(typeof(SubscriptionRenewalRequest), renewalFk.PrincipalEntityType.ClrType);
            Assert.False(renewalFk.IsRequired);
            Assert.Equal(DeleteBehavior.Restrict, renewalFk.DeleteBehavior);
        }

        [Fact]
        public void SubscriptionRenewalRequestsTable_MatchesTheErd()
        {
            var entity = FindEntity<SubscriptionRenewalRequest>();
            Assert.Equal("subscription_renewal_requests", entity.GetTableName());
            Assert.Equal("pk_subscription_renewal_requests", entity.FindPrimaryKey()!.GetName());
        }

        [Fact]
        public void SubscriptionRenewalRequestsColumns_MatchTheErd()
        {
            var entity = FindEntity<SubscriptionRenewalRequest>();

            AssertColumn(entity, nameof(SubscriptionRenewalRequest.Id), "id", typeof(Guid), nullable: false);
            AssertColumn(entity, nameof(SubscriptionRenewalRequest.SubscriptionId), "subscription_id", typeof(Guid), nullable: false);
            AssertColumn(entity, nameof(SubscriptionRenewalRequest.PlanVersionId), "plan_version_id", typeof(Guid), nullable: false);
            AssertColumn(entity, nameof(SubscriptionRenewalRequest.ProposedPrice), "proposed_price", typeof(decimal), nullable: false);
            AssertColumn(entity, nameof(SubscriptionRenewalRequest.ProposedCurrencyCode), "proposed_currency_code", typeof(string), nullable: false);
            AssertColumn(entity, nameof(SubscriptionRenewalRequest.Status), "status", typeof(RenewalRequestStatus), nullable: false);
            AssertColumn(entity, nameof(SubscriptionRenewalRequest.RequestedAt), "requested_at", typeof(DateTimeOffset), nullable: false);
            AssertColumn(entity, nameof(SubscriptionRenewalRequest.ExpiresAt), "expires_at", typeof(DateTimeOffset), nullable: false);
            AssertColumn(entity, nameof(SubscriptionRenewalRequest.DecidedAt), "decided_at", typeof(DateTimeOffset?), nullable: true);
            AssertColumn(entity, nameof(SubscriptionRenewalRequest.DecidedBy), "decided_by", typeof(string), nullable: true);
        }

        [Fact]
        public void SubscriptionRenewalRequestsIndexesAndChecks_MatchTheErd()
        {
            var entity = FindEntity<SubscriptionRenewalRequest>();

            var openIndex = FindIndex(entity, nameof(SubscriptionRenewalRequest.SubscriptionId));
            Assert.True(openIndex.IsUnique);
            Assert.Equal("uq_subscription_renewal_requests_open", openIndex.GetDatabaseName());
            Assert.Equal("\"status\" IN ('Pending', 'Approved')", openIndex.GetFilter());

            var checks = entity.GetCheckConstraints().ToDictionary(c => c.Name!, c => c.Sql);
            Assert.Contains(checks, c => c.Key == "ck_subscription_renewal_requests_status" && c.Value == "status IN ('Pending', 'Approved', 'Declined', 'Expired', 'Completed')");
            Assert.Contains(checks, c => c.Key == "ck_subscription_renewal_requests_expiry" && c.Value == "expires_at > requested_at");
            Assert.Contains(checks, c => c.Key == "ck_subscription_renewal_requests_decided" && c.Value == "(status = 'Pending') = (decided_at IS NULL)");
            Assert.Contains(checks, c => c.Key == "ck_subscription_renewal_requests_price_non_negative" && c.Value == "proposed_price >= 0");
            Assert.Contains(checks, c => c.Key == "ck_subscription_renewal_requests_currency_code_format" && c.Value == "proposed_currency_code ~ '^[a-z]{3}$'");
            Assert.Contains(checks, c => c.Key == "ck_subscription_renewal_requests_decided_by" && c.Value == "decided_by IS NULL OR char_length(decided_by) BETWEEN 1 AND 256");
        }

        [Fact]
        public void SubscriptionHistoryTable_MatchesTheErd()
        {
            var entity = FindEntity<SubscriptionHistory>();

            Assert.Equal("subscription_history", entity.GetTableName());
            Assert.Equal("pk_subscription_history", entity.FindPrimaryKey()!.GetName());
        }

        [Fact]
        public void SubscriptionHistoryColumns_MatchTheErd()
        {
            var entity = FindEntity<SubscriptionHistory>();

            AssertColumn(entity, nameof(SubscriptionHistory.Id), "id", typeof(Guid), nullable: false);
            AssertColumn(entity, nameof(SubscriptionHistory.SubscriptionId), "subscription_id", typeof(Guid), nullable: false);
            AssertColumn(entity, nameof(SubscriptionHistory.FromStatus), "from_status", typeof(SubscriptionStatus?), nullable: true);
            AssertColumn(entity, nameof(SubscriptionHistory.ToStatus), "to_status", typeof(SubscriptionStatus), nullable: false);
            AssertColumn(entity, nameof(SubscriptionHistory.PlanVersionId), "plan_version_id", typeof(Guid?), nullable: true);
            AssertColumn(entity, nameof(SubscriptionHistory.PeriodNumber), "period_number", typeof(int?), nullable: true);
            AssertColumn(entity, nameof(SubscriptionHistory.ActorPlatformAdminId), "actor_platform_admin_id", typeof(Guid?), nullable: true);
            AssertColumn(entity, nameof(SubscriptionHistory.ActorReference), "actor_reference", typeof(string), nullable: true);
            AssertColumn(entity, nameof(SubscriptionHistory.Reason), "reason", typeof(string), nullable: true);
            AssertColumn(entity, nameof(SubscriptionHistory.OccurredAt), "occurred_at", typeof(DateTimeOffset), nullable: false);
            AssertColumn(entity, nameof(SubscriptionHistory.CreatedAt), "created_at", typeof(DateTimeOffset), nullable: false);
        }

        [Fact]
        public void SubscriptionHistoryCheckConstraints_MatchTheErd()
        {
            var entity = FindEntity<SubscriptionHistory>();
            var checks = entity.GetCheckConstraints().ToDictionary(c => c.Name!, c => c.Sql);

            Assert.Contains(checks, c => c.Key == "ck_subscription_history_from_status" && c.Value == "from_status IS NULL OR from_status IN ('Active', 'Suspended', 'Expired', 'Cancelled')");
            Assert.Contains(checks, c => c.Key == "ck_subscription_history_to_status" && c.Value == "to_status IN ('Active', 'Suspended', 'Expired', 'Cancelled')");
            Assert.Contains(checks, c => c.Key == "ck_subscription_history_period_number" && c.Value == "period_number IS NULL OR period_number >= 1");
            Assert.Contains(checks, c => c.Key == "ck_subscription_history_actor_reference");
            Assert.Contains(checks, c => c.Key == "ck_subscription_history_reason");
        }

        [Fact]
        public void SubscriptionHistoryIndexesAndForeignKeys_MatchTheErd()
        {
            var entity = FindEntity<SubscriptionHistory>();

            Assert.Equal(
                "ix_subscription_history_subscription_occurred_at",
                FindIndex(
                    entity,
                    nameof(SubscriptionHistory.SubscriptionId),
                    nameof(SubscriptionHistory.OccurredAt)).GetDatabaseName());
            Assert.Equal(
                "ix_subscription_history_plan_version_id",
                FindIndex(entity, nameof(SubscriptionHistory.PlanVersionId)).GetDatabaseName());
            Assert.Equal(
                "ix_subscription_history_actor_platform_admin_id",
                FindIndex(entity, nameof(SubscriptionHistory.ActorPlatformAdminId)).GetDatabaseName());

            var foreignKeys = entity.GetForeignKeys().ToDictionary(fk => fk.GetConstraintName()!);
            Assert.Equal(typeof(Subscription), foreignKeys["fk_subscription_history_subscription_id"].PrincipalEntityType.ClrType);
            Assert.True(foreignKeys["fk_subscription_history_subscription_id"].IsRequired);
            Assert.Equal(typeof(PlanVersion), foreignKeys["fk_subscription_history_plan_version_id"].PrincipalEntityType.ClrType);
            Assert.False(foreignKeys["fk_subscription_history_plan_version_id"].IsRequired);
            Assert.Equal(
                typeof(CafeHub.Infrastructure.Persistence.Master.Identity.PlatformAdminUser),
                foreignKeys["fk_subscription_history_actor_platform_admin_id"].PrincipalEntityType.ClrType);
            Assert.False(foreignKeys["fk_subscription_history_actor_platform_admin_id"].IsRequired);
            Assert.All(foreignKeys.Values, fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
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
