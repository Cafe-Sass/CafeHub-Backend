using CafeHub.Application.Subscriptions;
using CafeHub.Domain.Plans;
using CafeHub.Domain.Subscriptions;

namespace CafeHub.UnitTests.Subscriptions.Domain
{
    public sealed class SubscriptionRenewalRequestTests
    {
        private static readonly Guid SubscriptionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid PlanVersionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly DateTimeOffset Now = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);

        [Fact]
        public void CreateStartsPendingWithTheProposedTermsAndExpiry()
        {
            var request = Create();

            Assert.Equal(SubscriptionId, request.SubscriptionId);
            Assert.Equal(PlanVersionId, request.PlanVersionId);
            Assert.Equal(49.99m, request.ProposedPrice);
            Assert.Equal("usd", request.ProposedCurrencyCode);
            Assert.Equal(RenewalRequestStatus.Pending, request.Status);
            Assert.Equal(Now, request.RequestedAt);
            Assert.Equal(Now.AddDays(7), request.ExpiresAt);
            Assert.Null(request.DecidedAt);
            Assert.Null(request.DecidedBy);
        }

        [Fact]
        public void ApprovalDeclineExpiryAndCompletionFollowTheStateMachine()
        {
            var request = Create();

            Assert.True(request.Approve(Now.AddHours(1), "owner-1"));
            Assert.Equal(RenewalRequestStatus.Approved, request.Status);
            Assert.Equal(Now.AddHours(1), request.DecidedAt);
            Assert.Equal("owner-1", request.DecidedBy);
            Assert.False(request.Approve(Now.AddHours(2), "owner-1"));
            Assert.False(request.Decline(Now.AddHours(2), "owner-1"));
            Assert.True(request.Complete(Now.AddHours(3)));
            Assert.Equal(RenewalRequestStatus.Completed, request.Status);
            Assert.False(request.Complete(Now.AddHours(4)));

            var declined = Create();
            Assert.True(declined.Decline(Now.AddHours(1), "owner-1"));
            Assert.Equal(RenewalRequestStatus.Declined, declined.Status);
            Assert.False(declined.Approve(Now.AddHours(2), "owner-1"));

            var expired = Create();
            Assert.False(expired.Expire(Now.AddDays(6)));
            Assert.True(expired.IsExpired(Now.AddDays(7)));
            Assert.True(expired.Expire(Now.AddDays(7)));
            Assert.Equal(RenewalRequestStatus.Expired, expired.Status);
            Assert.False(expired.Approve(Now.AddDays(8), "owner-1"));
            Assert.False(expired.Decline(Now.AddDays(8), "owner-1"));
        }

        [Fact]
        public void ThePlanCanChangeOnlyWhilePending()
        {
            var request = Create();
            var otherPlan = Guid.CreateVersion7();

            Assert.True(request.ChangePlan(otherPlan, 59.99m, "eur", Now.AddHours(1)));
            Assert.Equal(otherPlan, request.PlanVersionId);
            Assert.Equal(59.99m, request.ProposedPrice);
            Assert.Equal("eur", request.ProposedCurrencyCode);

            Assert.True(request.Approve(Now.AddHours(2), "owner-1"));
            Assert.False(request.ChangePlan(Guid.CreateVersion7(), 10m, "usd", Now.AddHours(3)));
        }

        [Fact]
        public void CreateRejectsInvalidTermsAndExpiry()
        {
            Assert.Throws<ArgumentException>(() => SubscriptionRenewalRequest.Create(
                Guid.Empty, SubscriptionId, PlanVersionId, 1m, "usd", Now, Now.AddDays(1)));
            Assert.Throws<ArgumentException>(() => SubscriptionRenewalRequest.Create(
                Guid.CreateVersion7(), SubscriptionId, Guid.Empty, 1m, "usd", Now, Now.AddDays(1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => SubscriptionRenewalRequest.Create(
                Guid.CreateVersion7(), SubscriptionId, PlanVersionId, -1m, "usd", Now, Now.AddDays(1)));
            Assert.Throws<ArgumentException>(() => SubscriptionRenewalRequest.Create(
                Guid.CreateVersion7(), SubscriptionId, PlanVersionId, 1m, "USD", Now, Now.AddDays(1)));
            Assert.Throws<ArgumentException>(() => SubscriptionRenewalRequest.Create(
                Guid.CreateVersion7(), SubscriptionId, PlanVersionId, 1m, "usd", Now, Now));
        }

        [Fact]
        public void RenewalPricingUsesTheBillingPeriod()
        {
            Assert.Equal(Now.AddMonths(1), SubscriptionRenewalPricing.NextEnd(Now, BillingPeriod.Monthly));
            Assert.Equal(Now.AddYears(1), SubscriptionRenewalPricing.NextEnd(Now, BillingPeriod.Annual));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => SubscriptionRenewalPricing.NextEnd(Now, (BillingPeriod)99));
        }

        [Fact]
        public void AnActivePeriodClosesOnceBeforeTheNextOneStarts()
        {
            var period = SubscriptionPeriod.Create(
                Guid.CreateVersion7(), SubscriptionId, PlanVersionId, 1,
                SubscriptionPeriodStatus.Active, 49.99m, "usd", Now.AddDays(-30), null, null,
                Now.AddDays(-30));

            Assert.True(period.End(Now, Now, null));
            Assert.Equal(SubscriptionPeriodStatus.Ended, period.PeriodStatus);
            Assert.Equal(Now, period.EndsAt);
            Assert.False(period.End(Now.AddDays(1), Now, null));
        }

        private static SubscriptionRenewalRequest Create() =>
            SubscriptionRenewalRequest.Create(
                Guid.CreateVersion7(),
                SubscriptionId,
                PlanVersionId,
                49.99m,
                "usd",
                Now,
                Now.AddDays(7));
    }
}
