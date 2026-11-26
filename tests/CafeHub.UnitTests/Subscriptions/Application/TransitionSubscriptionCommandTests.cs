using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Audit;
using CafeHub.Application.Identity;
using CafeHub.Application.Subscriptions;
using CafeHub.Application.Subscriptions.Commands;
using CafeHub.Application.Subscriptions.Contracts;
using CafeHub.Domain.Subscriptions;

namespace CafeHub.UnitTests.Subscriptions.Application
{
    public sealed class TransitionSubscriptionCommandTests
    {
        private static readonly Guid SubscriptionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid PlanVersionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid AdminId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly DateTimeOffset Now = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);

        [Fact]
        public async Task AnAllowedTransitionWritesOneHistoryRowAndOnePlatformAudit()
        {
            var subscription = Subscription.Create(
                SubscriptionId, Guid.CreateVersion7(), null, SubscriptionStatus.Active, Now.AddDays(-10));
            var period = SubscriptionPeriod.Create(
                Guid.CreateVersion7(), SubscriptionId, PlanVersionId, 1,
                SubscriptionPeriodStatus.Active, 49.99m, "usd", Now.AddDays(-10), null, null,
                Now.AddDays(-10));
            var store = new FakeStore(subscription, period);
            var unitOfWork = new FakeUnitOfWork();
            var audit = new FakeAuditWriter();
            var handler = new TransitionSubscriptionCommandHandler(
                store, unitOfWork, audit, new FakeCurrentUser(AdminId), new FixedClock(Now));

            var result = await handler.Handle(
                new TransitionSubscriptionCommand(
                    SubscriptionId, SubscriptionStatus.Suspended, "non_payment"),
                default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Equal(SubscriptionStatus.Active.ToString(), result.Value.PreviousStatus);
            Assert.Equal(SubscriptionStatus.Suspended.ToString(), result.Value.Status);
            Assert.Equal(Now, result.Value.OccurredAt);
            Assert.Equal(SubscriptionStatus.Suspended, subscription.Status);
            Assert.Single(store.History);
            var history = store.History[0];
            Assert.Equal(SubscriptionStatus.Active, history.FromStatus);
            Assert.Equal(SubscriptionStatus.Suspended, history.ToStatus);
            Assert.Equal(PlanVersionId, history.PlanVersionId);
            Assert.Equal(1, history.PeriodNumber);
            Assert.Equal(AdminId, history.ActorPlatformAdminId);
            Assert.Equal("non_payment", history.Reason);
            Assert.Equal(Now, history.OccurredAt);
            var entry = Assert.Single(audit.Entries);
            Assert.Equal("subscription.lifecycle.succeeded", entry.Action);
            Assert.Equal("subscription", entry.TargetType);
            Assert.Equal(SubscriptionId, entry.TargetId);
            Assert.Equal(PlatformAuditActorKind.Admin, entry.ActorKind);
            Assert.Equal(1, unitOfWork.BeginCount);
            Assert.Equal(1, unitOfWork.CommitCount);
            Assert.Equal(0, unitOfWork.RollbackCount);
        }

        [Fact]
        public async Task AForbiddenTransitionIsRefusedBeforeAnyWrite()
        {
            var subscription = Subscription.Create(
                SubscriptionId, Guid.CreateVersion7(), null, SubscriptionStatus.Cancelled, Now.AddDays(-10));
            var store = new FakeStore(subscription, null);
            var unitOfWork = new FakeUnitOfWork();
            var audit = new FakeAuditWriter();
            var handler = new TransitionSubscriptionCommandHandler(
                store, unitOfWork, audit, new FakeCurrentUser(AdminId), new FixedClock(Now));

            var result = await handler.Handle(
                new TransitionSubscriptionCommand(SubscriptionId, SubscriptionStatus.Active, null),
                default);

            Assert.Equal(SubscriptionErrors.InvalidTransition(), result.Error);
            Assert.Equal(SubscriptionStatus.Cancelled, subscription.Status);
            Assert.Empty(store.History);
            Assert.Empty(audit.Entries);
            Assert.Equal(0, unitOfWork.BeginCount);
        }

        [Fact]
        public async Task AMissingSubscriptionIsReportedAsNotFound()
        {
            var store = new FakeStore(null, null);
            var handler = new TransitionSubscriptionCommandHandler(
                store,
                new FakeUnitOfWork(),
                new FakeAuditWriter(),
                new FakeCurrentUser(AdminId),
                new FixedClock(Now));

            var result = await handler.Handle(
                new TransitionSubscriptionCommand(
                    SubscriptionId, SubscriptionStatus.Suspended, null),
                default);

            Assert.Equal(SubscriptionErrors.NotFound(), result.Error);
        }

        [Fact]
        public async Task ASystemTransitionRecordsNoActorAndAuditsTheSystemKind()
        {
            var subscription = Subscription.Create(
                SubscriptionId, Guid.CreateVersion7(), null, SubscriptionStatus.Active, Now.AddDays(-10));
            var store = new FakeStore(subscription, null);
            var audit = new FakeAuditWriter();
            var handler = new TransitionSubscriptionCommandHandler(
                store,
                new FakeUnitOfWork(),
                audit,
                new FakeCurrentUser(null),
                new FixedClock(Now));

            var result = await handler.Handle(
                new TransitionSubscriptionCommand(
                    SubscriptionId, SubscriptionStatus.Suspended, "period_ended"),
                default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            var history = Assert.Single(store.History);
            Assert.Null(history.ActorPlatformAdminId);
            Assert.Equal(PlatformAuditActorKind.System, Assert.Single(audit.Entries).ActorKind);
        }

        [Fact]
        public void TheTransitionValidatorEnforcesTheCommandShape()
        {
            var validator = new TransitionSubscriptionCommandValidator();

            Assert.True(validator.Validate(new TransitionSubscriptionCommand(
                SubscriptionId, SubscriptionStatus.Suspended, "non_payment")).IsValid);
            Assert.False(validator.Validate(new TransitionSubscriptionCommand(
                Guid.Empty, SubscriptionStatus.Suspended, null)).IsValid);
            Assert.False(validator.Validate(new TransitionSubscriptionCommand(
                SubscriptionId, (SubscriptionStatus)99, null)).IsValid);
            Assert.False(validator.Validate(new TransitionSubscriptionCommand(
                SubscriptionId, SubscriptionStatus.Suspended, new string('a', 257))).IsValid);
            Assert.False(validator.Validate(new TransitionSubscriptionCommand(
                SubscriptionId, SubscriptionStatus.Suspended, "bad\u0007reason")).IsValid);
        }

        private sealed class FakeStore(Subscription? subscription, SubscriptionPeriod? period)
            : ISubscriptionLifecycleStore
        {
            public List<SubscriptionHistory> History { get; } = [];

            public Task<Subscription?> FindAsync(
                Guid subscriptionId,
                CancellationToken cancellationToken) =>
                Task.FromResult(subscription);

            public Task<SubscriptionPeriod?> FindLatestPeriodAsync(
                Guid subscriptionId,
                CancellationToken cancellationToken) =>
                Task.FromResult(period);

            public void AddHistory(SubscriptionHistory history) => History.Add(history);
        }

        private sealed class FakeUnitOfWork : IMasterUnitOfWork
        {
            public int BeginCount { get; private set; }

            public int CommitCount { get; private set; }

            public int RollbackCount { get; private set; }

            public Task BeginTransactionAsync(CancellationToken cancellationToken)
            {
                BeginCount++;
                return Task.CompletedTask;
            }

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

        private sealed class FakeCurrentUser(Guid? adminId) : ICurrentUser
        {
            public bool IsAuthenticated => adminId is not null;

            public Guid? AdminId => adminId;

            public IReadOnlyCollection<string> Roles => [];
        }

        private sealed class FixedClock(DateTimeOffset now) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => now;
        }
    }
}
