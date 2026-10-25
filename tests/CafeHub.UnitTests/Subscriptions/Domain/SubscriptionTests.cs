using CafeHub.Domain.Subscriptions;

namespace CafeHub.UnitTests.Subscriptions.Domain
{
    public sealed class SubscriptionTests
    {
        [Fact]
        public void Create_SetsAllProperties()
        {
            var id = Guid.CreateVersion7();
            var requestId = Guid.CreateVersion7();
            var createdAt = new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);

            var subscription = Subscription.Create(id, requestId, null, SubscriptionStatus.Active, createdAt);

            Assert.Equal(id, subscription.Id);
            Assert.Equal(requestId, subscription.OnboardingRequestId);
            Assert.Null(subscription.TenantId);
            Assert.Equal(SubscriptionStatus.Active, subscription.Status);
            Assert.Equal(createdAt, subscription.CreatedAt);
            Assert.Equal(createdAt, subscription.UpdatedAt);
            Assert.Null(subscription.CreatedBy);
            Assert.Null(subscription.UpdatedBy);
        }

        [Fact]
        public void Create_WithTenantId_SetsTenantAnchor()
        {
            var id = Guid.CreateVersion7();
            var tenantId = Guid.CreateVersion7();
            var createdAt = new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);

            var subscription = Subscription.Create(id, null, tenantId, SubscriptionStatus.Active, createdAt);

            Assert.Null(subscription.OnboardingRequestId);
            Assert.Equal(tenantId, subscription.TenantId);
        }

        [Fact]
        public void Create_RejectsEmptyId()
        {
            Assert.Throws<ArgumentException>(() =>
                Subscription.Create(Guid.Empty, Guid.CreateVersion7(), null, SubscriptionStatus.Active, DateTimeOffset.UtcNow));
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData("00000000-0000-0000-0000-000000000000", "00000000-0000-0000-0000-000000000000")]
        public void Create_RejectsNeitherOrBothAnchors(string? requestId, string? tenantId)
        {
            var onboardingRequestId = requestId is null ? (Guid?)null : Guid.Parse(requestId);
            var tId = tenantId is null ? (Guid?)null : Guid.Parse(tenantId);

            Assert.Throws<ArgumentException>(() =>
                Subscription.Create(Guid.CreateVersion7(), onboardingRequestId, tId, SubscriptionStatus.Active, DateTimeOffset.UtcNow));
        }

        [Fact]
        public void Create_RejectsBothAnchorsSet()
        {
            Assert.Throws<ArgumentException>(() =>
                Subscription.Create(
                    Guid.CreateVersion7(),
                    Guid.CreateVersion7(),
                    Guid.CreateVersion7(),
                    SubscriptionStatus.Active,
                    DateTimeOffset.UtcNow));
        }
    }

    public sealed class SubscriptionPeriodTests
    {
        [Fact]
        public void Create_SetsAllProperties()
        {
            var id = Guid.CreateVersion7();
            var subscriptionId = Guid.CreateVersion7();
            var planVersionId = Guid.CreateVersion7();
            var startsAt = new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);
            var endsAt = new DateTimeOffset(2026, 11, 21, 10, 0, 0, TimeSpan.Zero);

            var period = SubscriptionPeriod.Create(
                id, subscriptionId, planVersionId, 1, SubscriptionPeriodStatus.Active,
                49.99m, "usd", startsAt, endsAt, null, startsAt);

            Assert.Equal(id, period.Id);
            Assert.Equal(subscriptionId, period.SubscriptionId);
            Assert.Equal(planVersionId, period.PlanVersionId);
            Assert.Equal(1, period.PeriodNumber);
            Assert.Equal(SubscriptionPeriodStatus.Active, period.PeriodStatus);
            Assert.Equal(49.99m, period.AgreedPrice);
            Assert.Equal("usd", period.CurrencyCode);
            Assert.Equal(startsAt, period.StartsAt);
            Assert.Equal(endsAt, period.EndsAt);
            Assert.Null(period.RenewalRequestId);
            Assert.Equal(startsAt, period.CreatedAt);
            Assert.Equal(startsAt, period.UpdatedAt);
        }

        [Fact]
        public void Create_RejectsEmptySubscriptionId()
        {
            Assert.Throws<ArgumentException>(() =>
                SubscriptionPeriod.Create(
                    Guid.CreateVersion7(), Guid.Empty, Guid.CreateVersion7(), 1,
                    SubscriptionPeriodStatus.Active, 49.99m, "usd",
                    DateTimeOffset.UtcNow, null, null, DateTimeOffset.UtcNow));
        }

        [Fact]
        public void Create_RejectsPeriodNumberLessThanOne()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SubscriptionPeriod.Create(
                    Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), 0,
                    SubscriptionPeriodStatus.Active, 49.99m, "usd",
                    DateTimeOffset.UtcNow, null, null, DateTimeOffset.UtcNow));
        }

        [Fact]
        public void Create_RejectsNegativePrice()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SubscriptionPeriod.Create(
                    Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), 1,
                    SubscriptionPeriodStatus.Active, -1.00m, "usd",
                    DateTimeOffset.UtcNow, null, null, DateTimeOffset.UtcNow));
        }

        [Theory]
        [InlineData("USD")]
        [InlineData("US")]
        [InlineData("usd1")]
        [InlineData("")]
        public void Create_RejectsInvalidCurrencyCode(string currencyCode)
        {
            Assert.Throws<ArgumentException>(() =>
                SubscriptionPeriod.Create(
                    Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), 1,
                    SubscriptionPeriodStatus.Active, 49.99m, currencyCode,
                    DateTimeOffset.UtcNow, null, null, DateTimeOffset.UtcNow));
        }

        [Fact]
        public void Create_RejectsEndsAtBeforeStartsAt()
        {
            var startsAt = new DateTimeOffset(2026, 11, 21, 10, 0, 0, TimeSpan.Zero);
            var endsAt = new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);

            Assert.Throws<ArgumentException>(() =>
                SubscriptionPeriod.Create(
                    Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), 1,
                    SubscriptionPeriodStatus.Active, 49.99m, "usd",
                    startsAt, endsAt, null, startsAt));
        }

        [Fact]
        public void Create_AcceptsNullEndsAt()
        {
            var startsAt = new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);

            var period = SubscriptionPeriod.Create(
                Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), 1,
                SubscriptionPeriodStatus.Active, 49.99m, "usd",
                startsAt, null, null, startsAt);

            Assert.Null(period.EndsAt);
        }
    }

    public sealed class PlatformPaymentTransactionTests
    {
        [Fact]
        public void Create_SetsAllProperties()
        {
            var id = Guid.CreateVersion7();
            var requestId = Guid.CreateVersion7();
            var createdAt = new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);

            var transaction = PlatformPaymentTransaction.Create(
                id, requestId, null, "key-12345678", 49.99m, "usd",
                PlatformPaymentTransactionStatus.Succeeded, createdAt, null, createdAt);

            Assert.Equal(id, transaction.Id);
            Assert.Equal(requestId, transaction.OnboardingRequestId);
            Assert.Null(transaction.RenewalRequestId);
            Assert.Equal("key-12345678", transaction.IdempotencyKey);
            Assert.Equal(49.99m, transaction.Amount);
            Assert.Equal("usd", transaction.CurrencyCode);
            Assert.Equal(PlatformPaymentTransactionStatus.Succeeded, transaction.Status);
            Assert.Equal(createdAt, transaction.SettledAt);
            Assert.Null(transaction.FailureCode);
            Assert.Equal(createdAt, transaction.CreatedAt);
            Assert.Equal(createdAt, transaction.UpdatedAt);
        }

        [Fact]
        public void Create_RejectsEmptyId()
        {
            Assert.Throws<ArgumentException>(() =>
                PlatformPaymentTransaction.Create(
                    Guid.Empty, Guid.CreateVersion7(), null, "key-12345678", 49.99m, "usd",
                    PlatformPaymentTransactionStatus.Succeeded, null, null, DateTimeOffset.UtcNow));
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData("00000000-0000-0000-0000-000000000000", "00000000-0000-0000-0000-000000000000")]
        public void Create_RejectsNeitherOrBothTargets(string? requestId, string? renewalId)
        {
            var orId = requestId is null ? (Guid?)null : Guid.Parse(requestId);
            var rrId = renewalId is null ? (Guid?)null : Guid.Parse(renewalId);

            Assert.Throws<ArgumentException>(() =>
                PlatformPaymentTransaction.Create(
                    Guid.CreateVersion7(), orId, rrId, "key-12345678", 49.99m, "usd",
                    PlatformPaymentTransactionStatus.Succeeded, null, null, DateTimeOffset.UtcNow));
        }

        [Fact]
        public void Create_RejectsShortIdempotencyKey()
        {
            Assert.Throws<ArgumentException>(() =>
                PlatformPaymentTransaction.Create(
                    Guid.CreateVersion7(), Guid.CreateVersion7(), null, "short", 49.99m, "usd",
                    PlatformPaymentTransactionStatus.Succeeded, null, null, DateTimeOffset.UtcNow));
        }

        [Fact]
        public void Create_RejectsNegativeAmount()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                PlatformPaymentTransaction.Create(
                    Guid.CreateVersion7(), Guid.CreateVersion7(), null, "key-12345678", -1.00m, "usd",
                    PlatformPaymentTransactionStatus.Succeeded, null, null, DateTimeOffset.UtcNow));
        }

        [Fact]
        public void Create_AcceptsNullFailureCode()
        {
            var transaction = PlatformPaymentTransaction.Create(
                Guid.CreateVersion7(), Guid.CreateVersion7(), null, "key-12345678", 49.99m, "usd",
                PlatformPaymentTransactionStatus.Succeeded, null, null, DateTimeOffset.UtcNow);

            Assert.Null(transaction.FailureCode);
        }
    }
}
