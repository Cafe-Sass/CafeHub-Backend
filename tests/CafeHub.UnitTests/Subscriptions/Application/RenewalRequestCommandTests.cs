using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Notifications;
using CafeHub.Application.Onboarding.Contracts;
using CafeHub.Application.Onboarding.PaymentGateway;
using CafeHub.Application.Subscriptions;
using CafeHub.Application.Subscriptions.Commands;
using CafeHub.Application.Subscriptions.Contracts;
using CafeHub.Domain.Onboarding;
using CafeHub.Domain.Plans;
using CafeHub.Domain.Subscriptions;

namespace CafeHub.UnitTests.Subscriptions.Application
{
    public sealed class RenewalRequestCommandTests
    {
        private static readonly Guid SubscriptionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid TenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid OtherTenantId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        private static readonly Guid OnboardingRequestId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly DateTimeOffset Now = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);

        [Fact]
        public async Task ApprovalChargesOnceExtendsThePeriodAndWritesHistory()
        {
            var context = new RenewalContext();
            var handler = context.ApproveHandler();
            var request = context.PendingRequest();

            var result = await handler.Handle(
                new ApproveRenewalRequestCommand(request.Id, TenantId, "owner-1"), default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Equal("Completed", result.Value.Status);
            Assert.NotNull(result.Value.PeriodId);
            Assert.Single(context.Gateway.Charges);
            Assert.Equal(49.99m, context.Gateway.Charges[0].Amount);
            Assert.Equal("usd", context.Gateway.Charges[0].Currency);
            Assert.Equal($"renewal-{request.Id:D}", context.Gateway.Charges[0].IdempotencyKey);
            Assert.Equal(RenewalRequestStatus.Completed, request.Status);
            Assert.Equal(SubscriptionStatus.Active, context.Subscription.Status);
            Assert.Equal(2, context.Store.Periods.Count);
            Assert.Equal(SubscriptionPeriodStatus.Ended, context.Store.Periods[0].PeriodStatus);
            Assert.Equal(Now, context.Store.Periods[0].EndsAt);
            Assert.Equal(request.Id, context.Store.Periods[1].RenewalRequestId);
            Assert.Equal(2, context.Store.Periods[1].PeriodNumber);
            var history = Assert.Single(context.Store.History);
            Assert.Equal(SubscriptionStatus.Suspended, history.FromStatus);
            Assert.Equal(SubscriptionStatus.Active, history.ToStatus);
            Assert.Equal("owner-1", history.ActorReference);
            Assert.Equal("subscription.renewal.approved.succeeded", Assert.Single(context.Audit.Entries).Action);
            Assert.Equal(1, context.UnitOfWork.CommitCount);
        }

        [Fact]
        public async Task RepeatApprovalReturnsTheSameResultAndNeverChargesAgain()
        {
            var context = new RenewalContext();
            var handler = context.ApproveHandler();
            var request = context.PendingRequest();

            var first = await handler.Handle(new ApproveRenewalRequestCommand(request.Id, TenantId, "owner-1"), default);
            var second = await handler.Handle(new ApproveRenewalRequestCommand(request.Id, TenantId, "owner-1"), default);

            Assert.True(first.IsSuccess, first.Error?.Code);
            Assert.True(second.IsSuccess, second.Error?.Code);
            Assert.Equal(first.Value.PeriodId, second.Value.PeriodId);
            Assert.Single(context.Gateway.Charges);
            Assert.Single(context.PaymentTransactions.Transactions);
            Assert.Equal(2, context.Store.Periods.Count);
        }

        [Fact]
        public async Task ADeclinedChargeNeverCreatesAPeriodAndNeverChargesAgain()
        {
            var context = new RenewalContext();
            context.Gateway.Outcome = PaymentOutcome.Declined;
            var handler = context.ApproveHandler();
            var request = context.PendingRequest();

            var first = await handler.Handle(new ApproveRenewalRequestCommand(request.Id, TenantId, "owner-1"), default);
            var second = await handler.Handle(new ApproveRenewalRequestCommand(request.Id, TenantId, "owner-1"), default);

            Assert.Equal(SubscriptionErrors.RenewalChargeFailed(), first.Error);
            Assert.Equal(SubscriptionErrors.RenewalChargeFailed(), second.Error);
            Assert.Equal(RenewalRequestStatus.Approved, request.Status);
            Assert.Single(context.Gateway.Charges);
            Assert.Single(context.Store.Periods);
            Assert.Equal(PlatformPaymentTransactionStatus.Failed, Assert.Single(
                context.PaymentTransactions.Transactions).Status);
        }

        [Fact]
        public async Task AnExpiredRequestNeverCharges()
        {
            var context = new RenewalContext();
            var handler = context.ApproveHandler();
            var request = SubscriptionRenewalRequest.Create(
                Guid.CreateVersion7(),
                SubscriptionId,
                context.PlanVersion.Id,
                49.99m,
                "usd",
                Now.AddDays(-8),
                Now.AddDays(-1));
            context.Store.Requests.Add(request);

            var result = await handler.Handle(
                new ApproveRenewalRequestCommand(request.Id, TenantId, "owner-1"), default);

            Assert.Equal(SubscriptionErrors.RenewalExpired(), result.Error);
            Assert.Equal(RenewalRequestStatus.Expired, request.Status);
            Assert.Empty(context.Gateway.Charges);
            Assert.Equal("subscription.renewal.expired.succeeded", Assert.Single(context.Audit.Entries).Action);
        }

        [Fact]
        public async Task DecliningARequestNeverCharges()
        {
            var context = new RenewalContext();
            var request = context.PendingRequest();

            var result = await new DeclineRenewalRequestCommandHandler(
                    context.Store, context.UnitOfWork, context.Audit, context.Clock)
                .Handle(new DeclineRenewalRequestCommand(request.Id, TenantId, "owner-1"), default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Equal(RenewalRequestStatus.Declined, request.Status);
            Assert.Empty(context.Gateway.Charges);
            Assert.Equal("subscription.renewal.declined.succeeded", Assert.Single(context.Audit.Entries).Action);
        }

        [Fact]
        public async Task AnOwnerCannotApproveAnotherTenantsRenewal()
        {
            var context = new RenewalContext();
            var request = context.PendingRequest();

            var result = await context.ApproveHandler().Handle(
                new ApproveRenewalRequestCommand(request.Id, OtherTenantId, "owner-1"), default);

            Assert.Equal(SubscriptionErrors.RenewalRequestNotFound(), result.Error);
            Assert.Equal(RenewalRequestStatus.Pending, request.Status);
            Assert.Empty(context.Gateway.Charges);
            Assert.Empty(context.Audit.Entries);
            Assert.Equal(0, context.UnitOfWork.CommitCount);
        }

        [Fact]
        public async Task AnOwnerCannotDeclineAnotherTenantsRenewal()
        {
            var context = new RenewalContext();
            var request = context.PendingRequest();

            var result = await new DeclineRenewalRequestCommandHandler(
                    context.Store, context.UnitOfWork, context.Audit, context.Clock)
                .Handle(new DeclineRenewalRequestCommand(request.Id, OtherTenantId, "owner-1"), default);

            Assert.Equal(SubscriptionErrors.RenewalRequestNotFound(), result.Error);
            Assert.Equal(RenewalRequestStatus.Pending, request.Status);
            Assert.Empty(context.Gateway.Charges);
            Assert.Empty(context.Audit.Entries);
            Assert.Equal(0, context.UnitOfWork.CommitCount);
        }

        [Fact]
        public async Task ThePlanChangeAppliesAtTheNextRenewal()
        {
            var context = new RenewalContext();
            var request = context.PendingRequest();
            var otherPlan = context.AddPlanVersion(99.00m, "eur", BillingPeriod.Annual);

            var changed = await context.ChangePlanHandler()
                .Handle(new ChangeRenewalPlanCommand(SubscriptionId, otherPlan.Id, "owner-1"), default);
            Assert.True(changed.IsSuccess, changed.Error?.Code);
            Assert.Equal(99.00m, changed.Value.ProposedPrice);
            Assert.Equal("eur", changed.Value.ProposedCurrencyCode);

            var approved = await context.ApproveHandler()
                .Handle(new ApproveRenewalRequestCommand(request.Id, TenantId, "owner-1"), default);

            Assert.True(approved.IsSuccess, approved.Error?.Code);
            Assert.Equal(99.00m, context.Gateway.Charges[0].Amount);
            Assert.Equal("eur", context.Gateway.Charges[0].Currency);
            Assert.Equal(otherPlan.Id, context.Store.Periods[1].PlanVersionId);
            Assert.Equal(Now.AddYears(1), context.Store.Periods[1].EndsAt);
        }

        [Fact]
        public async Task CreatingARequestSnapshotsThePlanAndSendsTheNotice()
        {
            var context = new RenewalContext();
            context.Onboarding.Request = OnboardingRequest.CreateForPublicRegistration(
                OnboardingRequestId,
                context.PlanVersion.Id,
                "owner@example.com",
                "Owner Name",
                null,
                "Cafe One",
                "hash",
                "cafe-one",
                Now);

            var result = await context.CreateHandler().Handle(
                new CreateRenewalRequestCommand(SubscriptionId, context.PlanVersion.Id, "owner-1"),
                default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            var request = Assert.Single(context.Store.Requests);
            Assert.Equal(context.PlanVersion.Id, request.PlanVersionId);
            Assert.Equal(49.99m, request.ProposedPrice);
            Assert.Equal("usd", request.ProposedCurrencyCode);
            Assert.Equal(Now.AddDays(7), request.ExpiresAt);
            var notification = Assert.Single(context.Notifications.Messages);
            Assert.Equal(NotificationKind.RenewalRequested, notification.Kind);
            Assert.Equal("subscription.renewal.requested", notification.TemplateKey);
            Assert.Equal("subscription.renewal.requested.succeeded", Assert.Single(context.Audit.Entries).Action);
        }

        [Fact]
        public void TheRenewalValidatorsEnforceTheirCommandShape()
        {
            Assert.False(new CreateRenewalRequestCommandValidator()
                .Validate(new CreateRenewalRequestCommand(Guid.Empty, null, null)).IsValid);
            Assert.False(new ApproveRenewalRequestCommandValidator()
                .Validate(new ApproveRenewalRequestCommand(Guid.Empty, Guid.Empty, null)).IsValid);
            Assert.False(new DeclineRenewalRequestCommandValidator()
                .Validate(new DeclineRenewalRequestCommand(Guid.Empty, Guid.Empty, null)).IsValid);
            Assert.False(new ExpireRenewalRequestCommandValidator()
                .Validate(new ExpireRenewalRequestCommand(Guid.Empty)).IsValid);
            Assert.False(new ChangeRenewalPlanCommandValidator()
                .Validate(new ChangeRenewalPlanCommand(SubscriptionId, Guid.Empty, null)).IsValid);
            Assert.True(new ApproveRenewalRequestCommandValidator()
                .Validate(new ApproveRenewalRequestCommand(Guid.CreateVersion7(), TenantId, "owner-1")).IsValid);
        }

        private sealed class RenewalContext
        {
            public RenewalContext()
            {
                Subscription = Subscription.Create(
                    SubscriptionId, OnboardingRequestId, null, SubscriptionStatus.Suspended, Now);
                Subscription.LinkToTenant(TenantId, Now);
                Store = new FakeStore(Subscription);
                PlanVersion = AddPlanVersion(49.99m, "usd", BillingPeriod.Monthly);
            }

            public Subscription Subscription { get; set; }

            public PlanVersion PlanVersion { get; }

            public FakeStore Store { get; }

            public FakePaymentTransactionStore PaymentTransactions { get; } = new();

            public FakeGateway Gateway { get; } = new();

            public FakeUnitOfWork UnitOfWork { get; } = new();

            public FakeAuditWriter Audit { get; } = new();

            public FakeNotificationSender Notifications { get; } = new();

            public FakeOnboardingRequestStore Onboarding { get; } = new();

            public FixedClock Clock { get; } = new(Now);

            public PlanVersion AddPlanVersion(decimal price, string currency, BillingPeriod billingPeriod)
            {
                var plan = PlanVersion.Create(
                    Guid.CreateVersion7(),
                    Guid.CreateVersion7(),
                    1,
                    price,
                    currency,
                    billingPeriod,
                    PlanVersionStatus.Active,
                    [],
                    Now,
                    null);
                Store.PlanVersions[plan.Id] = plan;
                return plan;
            }

            public SubscriptionRenewalRequest PendingRequest()
            {
                var request = SubscriptionRenewalRequest.Create(
                    Guid.CreateVersion7(),
                    Subscription.Id,
                    PlanVersion.Id,
                    PlanVersion.Price,
                    PlanVersion.CurrencyCode,
                    Now,
                    Now.AddDays(7));
                Store.Requests.Add(request);
                var period = SubscriptionPeriod.Create(
                    Guid.CreateVersion7(),
                    Subscription.Id,
                    PlanVersion.Id,
                    1,
                    SubscriptionPeriodStatus.Active,
                    PlanVersion.Price,
                    PlanVersion.CurrencyCode,
                    Now.AddDays(-30),
                    null,
                    null,
                    Now.AddDays(-30));
                Store.Periods.Add(period);
                return request;
            }

            public SubscriptionRenewalCreator Creator() =>
                new(Store, Onboarding, Notifications, new SubscriptionOptions(), Clock);

            public CreateRenewalRequestCommandHandler CreateHandler() =>
                new(Store, Creator(), UnitOfWork, Audit);

            public ChangeRenewalPlanCommandHandler ChangePlanHandler() =>
                new(Store, new SubscriptionNextPlanService(Store, Creator(), Clock), UnitOfWork, Audit);

            public ApproveRenewalRequestCommandHandler ApproveHandler() =>
                new(Store, PaymentTransactions, Gateway, UnitOfWork, Audit, Clock);
        }

        private sealed class FakeStore(Subscription subscription) : ISubscriptionRenewalStore
        {
            public Subscription Subscription { get; } = subscription;

            public List<SubscriptionRenewalRequest> Requests { get; } = [];

            public List<SubscriptionPeriod> Periods { get; } = [];

            public List<SubscriptionHistory> History { get; } = [];

            public Dictionary<Guid, PlanVersion> PlanVersions { get; } = [];

            public Task<Subscription?> FindSubscriptionAsync(
                Guid subscriptionId, CancellationToken cancellationToken) =>
                Task.FromResult<Subscription?>(Subscription.Id == subscriptionId ? Subscription : null);

            public Task<Subscription?> FindSubscriptionByTenantAsync(
                Guid tenantId, CancellationToken cancellationToken) =>
                Task.FromResult<Subscription?>(
                    Subscription.TenantId == tenantId ? Subscription : null);

            public Task<SubscriptionRenewalRequest?> FindRequestAsync(
                Guid renewalRequestId, CancellationToken cancellationToken) =>
                Task.FromResult(Requests.FirstOrDefault(request => request.Id == renewalRequestId));

            public Task<SubscriptionRenewalRequest?> FindOpenRequestAsync(
                Guid subscriptionId, CancellationToken cancellationToken) =>
                Task.FromResult(Requests.FirstOrDefault(request =>
                    request.SubscriptionId == subscriptionId
                    && (request.Status == RenewalRequestStatus.Pending
                        || request.Status == RenewalRequestStatus.Approved)));

            public Task<SubscriptionPeriod?> FindLatestPeriodAsync(
                Guid subscriptionId, CancellationToken cancellationToken) =>
                Task.FromResult(Periods
                    .Where(period => period.SubscriptionId == subscriptionId)
                    .OrderByDescending(period => period.PeriodNumber)
                    .FirstOrDefault());

            public Task<SubscriptionPeriod?> FindPeriodByRenewalRequestAsync(
                Guid renewalRequestId, CancellationToken cancellationToken) =>
                Task.FromResult(Periods.FirstOrDefault(
                    period => period.RenewalRequestId == renewalRequestId));

            public Task<PlanVersion?> FindPlanVersionAsync(
                Guid planVersionId, CancellationToken cancellationToken) =>
                Task.FromResult(PlanVersions.GetValueOrDefault(planVersionId));

            public void AddRequest(SubscriptionRenewalRequest request) => Requests.Add(request);

            public void AddPeriod(SubscriptionPeriod period) => Periods.Add(period);

            public void AddHistory(SubscriptionHistory history) => History.Add(history);
        }

        private sealed class FakePaymentTransactionStore : IPaymentTransactionStore
        {
            public List<PlatformPaymentTransaction> Transactions { get; } = [];

            public Task<PlatformPaymentTransaction?> FindByIdempotencyKeyAsync(
                Guid onboardingRequestId, string idempotencyKey, CancellationToken cancellationToken) =>
                Task.FromResult<PlatformPaymentTransaction?>(null);

            public Task<PlatformPaymentTransaction?> FindByOnboardingRequestIdAsync(
                Guid onboardingRequestId, CancellationToken cancellationToken) =>
                Task.FromResult<PlatformPaymentTransaction?>(null);

            public Task<PlatformPaymentTransaction?> FindByRenewalRequestIdAsync(
                Guid renewalRequestId, CancellationToken cancellationToken) =>
                Task.FromResult(Transactions.FirstOrDefault(
                    transaction => transaction.RenewalRequestId == renewalRequestId));

            public Task<bool> KeyExistsAsync(string idempotencyKey, CancellationToken cancellationToken) =>
                Task.FromResult(Transactions.Any(transaction => transaction.IdempotencyKey == idempotencyKey));

            public Task AddAsync(PlatformPaymentTransaction transaction, CancellationToken cancellationToken)
            {
                Transactions.Add(transaction);
                return Task.CompletedTask;
            }
        }

        private sealed class FakeGateway : IPaymentGateway
        {
            public List<PaymentChargeRequest> Charges { get; } = [];

            public PaymentOutcome Outcome { get; set; } = PaymentOutcome.Succeeded;

            public Task<Result<PaymentChargeResult>> ChargeAsync(
                PaymentChargeRequest request, CancellationToken cancellationToken)
            {
                Charges.Add(request);
                return Task.FromResult(Result.Success(new PaymentChargeResult(
                    Outcome,
                    $"mock_{Guid.CreateVersion7()}",
                    Outcome == PaymentOutcome.Succeeded ? null : "insufficient_funds")));
            }
        }

        private sealed class FakeUnitOfWork : IMasterUnitOfWork
        {
            public int CommitCount { get; private set; }

            public int RollbackCount { get; private set; }

            public Task BeginTransactionAsync(CancellationToken cancellationToken) => Task.CompletedTask;

            public Task CommitTransactionAsync(CancellationToken cancellationToken)
            {
                CommitCount++;
                return Task.CompletedTask;
            }

            public Task RollbackTransactionAsync(CancellationToken cancellationToken)
            {
                RollbackCount++;
                return Task.CompletedTask;
            }
        }

        private sealed class FakeAuditWriter : IPlatformAuditWriter
        {
            public List<PlatformAuditEntry> Entries { get; } = [];

            public void Write(PlatformAuditEntry entry) => Entries.Add(entry);
        }

        private sealed class FakeNotificationSender : INotificationSender
        {
            public List<NotificationMessage> Messages { get; } = [];

            public Task<Result> SendAsync(NotificationMessage message, CancellationToken cancellationToken)
            {
                Messages.Add(message);
                return Task.FromResult(Result.Success());
            }
        }

        private sealed class FakeOnboardingRequestStore : IOnboardingRequestStore
        {
            public OnboardingRequest? Request { get; set; }

            public Task<OnboardingRequest?> FindPublicByEmailAsync(
                string emailLower, CancellationToken cancellationToken) =>
                Task.FromResult<OnboardingRequest?>(null);

            public Task<OnboardingRequest?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
                Task.FromResult(Request?.Id == id ? Request : null);

            public Task AddAsync(OnboardingRequest request, CancellationToken cancellationToken) =>
                Task.CompletedTask;

            public Task UpdateAsync(OnboardingRequest request, CancellationToken cancellationToken) =>
                Task.CompletedTask;

            public Task<OnboardingRequestListPage> ListForReviewAsync(
                OnboardingRequestListRequest request, CancellationToken cancellationToken) =>
                throw new NotSupportedException();
        }

        private sealed class FixedClock(DateTimeOffset now) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => now;
        }
    }
}
