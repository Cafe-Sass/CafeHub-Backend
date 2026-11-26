using CafeHub.Application.Subscriptions;
using CafeHub.Domain.Subscriptions;
using Microsoft.Extensions.Configuration;

namespace CafeHub.UnitTests.Subscriptions.Domain
{
    public sealed class SubscriptionLifecycleTests
    {
        private static readonly Guid SubscriptionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid PlanVersionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid AdminId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly DateTimeOffset Now = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);

        [Theory]
        [InlineData(SubscriptionStatus.Active, SubscriptionStatus.Suspended)]
        [InlineData(SubscriptionStatus.Active, SubscriptionStatus.Expired)]
        [InlineData(SubscriptionStatus.Active, SubscriptionStatus.Cancelled)]
        [InlineData(SubscriptionStatus.Suspended, SubscriptionStatus.Active)]
        [InlineData(SubscriptionStatus.Suspended, SubscriptionStatus.Expired)]
        [InlineData(SubscriptionStatus.Suspended, SubscriptionStatus.Cancelled)]
        [InlineData(SubscriptionStatus.Expired, SubscriptionStatus.Active)]
        [InlineData(SubscriptionStatus.Expired, SubscriptionStatus.Cancelled)]
        public void EveryAllowedTransitionSucceedsAndWritesTheState(
            SubscriptionStatus from, SubscriptionStatus to)
        {
            Assert.True(SubscriptionLifecycle.IsAllowed(from, to));

            var subscription = CreateSubscription(from);
            var changed = subscription.TransitionTo(to, Now.AddMinutes(5), AdminId);

            Assert.True(changed);
            Assert.Equal(to, subscription.Status);
            Assert.Equal(Now.AddMinutes(5), subscription.UpdatedAt);
            Assert.Equal(AdminId, subscription.UpdatedBy);
        }

        [Theory]
        [InlineData(SubscriptionStatus.Active, SubscriptionStatus.Active)]
        [InlineData(SubscriptionStatus.Suspended, SubscriptionStatus.Suspended)]
        [InlineData(SubscriptionStatus.Expired, SubscriptionStatus.Expired)]
        [InlineData(SubscriptionStatus.Cancelled, SubscriptionStatus.Cancelled)]
        [InlineData(SubscriptionStatus.Cancelled, SubscriptionStatus.Active)]
        [InlineData(SubscriptionStatus.Cancelled, SubscriptionStatus.Suspended)]
        [InlineData(SubscriptionStatus.Expired, SubscriptionStatus.Suspended)]
        public void EveryForbiddenTransitionIsRefused(SubscriptionStatus from, SubscriptionStatus to)
        {
            Assert.False(SubscriptionLifecycle.IsAllowed(from, to));

            var subscription = CreateSubscription(from);
            var changed = subscription.TransitionTo(to, Now, AdminId);

            Assert.False(changed);
            Assert.Equal(from, subscription.Status);
            Assert.Equal(Now.AddDays(-1), subscription.UpdatedAt);
        }

        [Fact]
        public void ATransitionNeverChangesTheTenantOrRequestAnchor()
        {
            var requestId = Guid.CreateVersion7();
            var subscription = Subscription.Create(
                SubscriptionId, requestId, null, SubscriptionStatus.Active, Now.AddDays(-1));

            Assert.True(subscription.TransitionTo(SubscriptionStatus.Suspended, Now, AdminId));

            Assert.Equal(requestId, subscription.OnboardingRequestId);
            Assert.Null(subscription.TenantId);
            Assert.Equal(SubscriptionStatus.Suspended, subscription.Status);
        }

        [Fact]
        public void HistoryCapturesStatePlanPeriodActorReasonAndTime()
        {
            var history = SubscriptionHistory.Create(
                Guid.CreateVersion7(),
                SubscriptionId,
                SubscriptionStatus.Active,
                SubscriptionStatus.Suspended,
                PlanVersionId,
                2,
                AdminId,
                "owner@example.test",
                "non_payment",
                Now);

            Assert.Equal(SubscriptionId, history.SubscriptionId);
            Assert.Equal(SubscriptionStatus.Active, history.FromStatus);
            Assert.Equal(SubscriptionStatus.Suspended, history.ToStatus);
            Assert.Equal(PlanVersionId, history.PlanVersionId);
            Assert.Equal(2, history.PeriodNumber);
            Assert.Equal(AdminId, history.ActorPlatformAdminId);
            Assert.Equal("owner@example.test", history.ActorReference);
            Assert.Equal("non_payment", history.Reason);
            Assert.Equal(Now, history.OccurredAt);
            Assert.Equal(Now, history.CreatedAt);

            var initial = SubscriptionHistory.Create(
                Guid.CreateVersion7(),
                SubscriptionId,
                null,
                SubscriptionStatus.Active,
                PlanVersionId,
                1,
                null,
                null,
                null,
                Now);

            Assert.Null(initial.FromStatus);
            Assert.Equal(SubscriptionStatus.Active, initial.ToStatus);
        }

        [Fact]
        public void HistoryRejectsARowWithoutAStateChangeOrWithInvalidValues()
        {
            Assert.Throws<ArgumentException>(() => SubscriptionHistory.Create(
                Guid.CreateVersion7(), SubscriptionId,
                SubscriptionStatus.Active, SubscriptionStatus.Active,
                PlanVersionId, 1, AdminId, null, null, Now));
            Assert.Throws<ArgumentException>(() => SubscriptionHistory.Create(
                Guid.Empty, SubscriptionId,
                SubscriptionStatus.Active, SubscriptionStatus.Suspended,
                PlanVersionId, 1, AdminId, null, null, Now));
            Assert.Throws<ArgumentOutOfRangeException>(() => SubscriptionHistory.Create(
                Guid.CreateVersion7(), SubscriptionId,
                SubscriptionStatus.Active, SubscriptionStatus.Suspended,
                PlanVersionId, 0, AdminId, null, null, Now));
            Assert.Throws<ArgumentException>(() => SubscriptionHistory.Create(
                Guid.CreateVersion7(), SubscriptionId,
                SubscriptionStatus.Active, SubscriptionStatus.Suspended,
                Guid.Empty, 1, AdminId, null, null, Now));
            Assert.Throws<ArgumentException>(() => SubscriptionHistory.Create(
                Guid.CreateVersion7(), SubscriptionId,
                SubscriptionStatus.Active, SubscriptionStatus.Suspended,
                PlanVersionId, 1, AdminId, " ", null, Now));
        }

        [Fact]
        public void OptionsHoldTheProposedDefaultsAndRejectNegativeValues()
        {
            var defaults = new SubscriptionOptions();

            Assert.Equal(3, defaults.GraceDays);
            Assert.Equal(90, defaults.SuspendedRetentionDays);
            Assert.Empty(defaults.Validate());

            var invalid = new SubscriptionOptions { GraceDays = -1, SuspendedRetentionDays = -2 };
            Assert.Equal(2, invalid.Validate().Count);
        }

        [Fact]
        public void TheOptionsBinderReadsValuesAndFailsClosed()
        {
            var bound = SubscriptionOptionsBinder.Bind(Configuration(new Dictionary<string, string?>
            {
                ["Subscription:GraceDays"] = "5",
                ["Subscription:SuspendedRetentionDays"] = "120",
            }));

            Assert.Equal(5, bound.GraceDays);
            Assert.Equal(120, bound.SuspendedRetentionDays);
            Assert.Equal(
                3,
                SubscriptionOptionsBinder.Bind(new ConfigurationBuilder().Build()).GraceDays);

            Assert.Throws<InvalidSubscriptionConfigurationException>(
                () => SubscriptionOptionsBinder.Bind(Configuration(new Dictionary<string, string?>
                {
                    ["Subscription:GraceDays"] = "-1",
                })));
            Assert.Throws<InvalidSubscriptionConfigurationException>(
                () => SubscriptionOptionsBinder.Bind(Configuration(new Dictionary<string, string?>
                {
                    ["Subscription:SuspendedRetentionDays"] = "not-a-number",
                })));
        }

        [Fact]
        public void NextStateAppliesGraceThenSuspensionRetention()
        {
            var options = new SubscriptionOptions { GraceDays = 3, SuspendedRetentionDays = 90 };
            var endsAt = Now.AddDays(-4);

            Assert.Equal(
                SubscriptionStatus.Active,
                SubscriptionLifecyclePolicy.NextState(
                    SubscriptionStatus.Active, Now.AddDays(-1), Now, options));
            Assert.Equal(
                SubscriptionStatus.Suspended,
                SubscriptionLifecyclePolicy.NextState(
                    SubscriptionStatus.Active, endsAt, Now, options));
            Assert.Equal(
                SubscriptionStatus.Suspended,
                SubscriptionLifecyclePolicy.NextState(
                    SubscriptionStatus.Suspended, endsAt, Now, options));
            Assert.Equal(
                SubscriptionStatus.Expired,
                SubscriptionLifecyclePolicy.NextState(
                    SubscriptionStatus.Suspended, Now.AddDays(-94), Now, options));
            Assert.Equal(
                SubscriptionStatus.Cancelled,
                SubscriptionLifecyclePolicy.NextState(
                    SubscriptionStatus.Cancelled, Now.AddDays(-400), Now, options));
            Assert.Equal(
                SubscriptionStatus.Active,
                SubscriptionLifecyclePolicy.NextState(
                    SubscriptionStatus.Active, null, Now, options));
        }

        private static Subscription CreateSubscription(SubscriptionStatus status) =>
            Subscription.Create(
                SubscriptionId,
                Guid.CreateVersion7(),
                null,
                status,
                Now.AddDays(-1));

        private static IConfiguration Configuration(IReadOnlyDictionary<string, string?> values) =>
            new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
