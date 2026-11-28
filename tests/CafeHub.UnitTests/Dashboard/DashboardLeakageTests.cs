using System.Threading;
using CafeHub.Application.CafeProfile.Contracts;
using CafeHub.Application.CafeProfile.DTOs;
using CafeHub.Application.Dashboard;
using CafeHub.Application.Dashboard.Contracts;
using CafeHub.Application.Dashboard.DTOs;
using CafeHub.Application.Dashboard.Queries;
using CafeHub.Application.Identity;
using CafeHub.Domain.Tenants;
using FluentValidation.TestHelper;

namespace CafeHub.UnitTests.Dashboard
{
    public sealed class DashboardLeakageTests
    {
        private static readonly Guid BranchA = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid BranchB = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid ActorId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly DateTimeOffset From = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        private static readonly DateTimeOffset To = new(2026, 12, 1, 0, 0, 0, TimeSpan.Zero);

        [Fact]
        public void RejectedAndExpiredOrdersInRange_ExcludeAcceptedPendingAndOutOfRange()
        {
            var rejected = RejectedOrder(BranchA, 25.50m, From.AddDays(2));
            var expired = ExpiredOrder(BranchA, 10m, From.AddDays(3));
            var accepted = AcceptedOrder(BranchA, 20m, From.AddDays(2));
            var pending = NewOrder(BranchA, 15m, From.AddDays(2), OrderServiceType.DineIn);
            var before = RejectedOrder(BranchA, 30m, From.AddMinutes(-1));
            var atTo = RejectedOrder(BranchA, 40m, To);
            var orders = new[] { rejected, expired, accepted, pending, before, atTo }.AsQueryable();

            var selected = orders.WhereRejectedOrExpiredInRange(null, From, To).ToList();

            Assert.Equal(2, selected.Count);
            Assert.Contains(selected, order => order.Id == rejected.Id);
            Assert.Contains(selected, order => order.Id == expired.Id);
        }

        [Fact]
        public void RejectedAndExpiredOrdersInRange_ApplyTheBranchFilterOnlyWhenOneIsRequested()
        {
            var branchA = RejectedOrder(BranchA, 10m, From.AddDays(1));
            var branchB = ExpiredOrder(BranchB, 20m, From.AddDays(1));
            var orders = new[] { branchA, branchB }.AsQueryable();

            var all = orders.WhereRejectedOrExpiredInRange(null, From, To).ToList();
            var onlyB = orders.WhereRejectedOrExpiredInRange(BranchB, From, To).ToList();

            Assert.Equal(2, all.Count);
            Assert.Equal(new[] { branchB.Id }, onlyB.Select(order => order.Id).ToArray());
        }

        [Fact]
        public void ClosedBillsInRange_ExcludeOpenBillsAndOutOfRange()
        {
            var closed = ClosedBill(BranchA, 50m, From.AddDays(3));
            var open = OpenBill(BranchA, From.AddDays(3));
            var before = ClosedBill(BranchA, 50m, From.AddMinutes(-1));
            var atTo = ClosedBill(BranchB, 50m, To);
            var bills = new[] { closed, open, before, atTo }.AsQueryable();

            var selected = bills.WhereClosedInRange(null, From, To).ToList();
            var onlyA = bills.WhereClosedInRange(BranchA, From, To).ToList();

            Assert.Equal(new[] { closed.Id }, selected.Select(bill => bill.Id).ToArray());
            Assert.Equal(new[] { closed.Id }, onlyA.Select(bill => bill.Id).ToArray());
        }

        [Fact]
        public void RejectedPaymentProofsInRange_ExcludeConfirmedPendingAndOutOfRange()
        {
            var rejected = RejectedProof(30m, From.AddDays(2));
            var pending = PendingProof(15m, From.AddDays(2));
            var confirmed = ConfirmedProof(20m, From.AddDays(2));
            var before = RejectedProof(40m, From.AddMinutes(-1));
            var atTo = RejectedProof(50m, To);
            var payments = new[] { rejected, pending, confirmed, before, atTo }.AsQueryable();

            var selected = payments.WhereRejectedInRange(From, To).ToList();

            Assert.Equal(new[] { rejected.Id }, selected.Select(payment => payment.Id).ToArray());
        }

        [Fact]
        public async Task ControlledDataset_ReturnsTheExactSignalCountsAndValues()
        {
            var store = new FakeLeakageStore(new DashboardLeakageSnapshot(
                new[]
                {
                    new DashboardLeakageRecord(Guid.NewGuid(), 25.50m, From.AddDays(2)),
                    new DashboardLeakageRecord(Guid.NewGuid(), 10m, From.AddDays(3)),
                },
                new[]
                {
                    new DashboardLeakageBillRecord(Guid.NewGuid(), 100m, 60m, From.AddDays(4)),
                    new DashboardLeakageBillRecord(Guid.NewGuid(), 50m, 50m, From.AddDays(5)),
                },
                new[]
                {
                    new DashboardLeakageRecord(Guid.NewGuid(), 30m, From.AddDays(6)),
                    new DashboardLeakageRecord(Guid.NewGuid(), 15m, From.AddDays(7)),
                }));

            var result = await Handler(store).Handle(Query(), CancellationToken.None);

            Assert.True(result.IsSuccess);
            var signals = result.Value.Signals;
            Assert.Equal(4, signals.Count);
            AssertSignal(signals, DashboardLeakageKeys.RejectedAndExpiredOrders, 2, 35.50m);
            AssertSignal(signals, DashboardLeakageKeys.BillsClosedWithAmountOwed, 1, 40m);
            AssertSignal(signals, DashboardLeakageKeys.RejectedPaymentProofs, 2, 45m);
            AssertSignal(signals, DashboardLeakageKeys.SalesCollectedGap, 1, 40m);
        }

        [Fact]
        public void BillsClosedWithAmountOwed_CountOnlyBillsWithAPositiveRemainingAmount()
        {
            var bills = new[]
            {
                new DashboardLeakageBillRecord(Guid.NewGuid(), 80m, 30m, From.AddDays(1)),
                new DashboardLeakageBillRecord(Guid.NewGuid(), 60m, 60m, From.AddDays(2)),
                new DashboardLeakageBillRecord(Guid.NewGuid(), 20m, 5m, From.AddDays(3)),
            };

            var signal = DashboardLeakageRules.BillsClosedWithAmountOwed(bills, 1);

            Assert.Equal(2, signal.Count);
            Assert.Equal(65m, signal.Value);
            Assert.Equal(new[] { 15m, 50m }, signal.Contributors.Select(row => row.Total).OrderBy(total => total).ToArray());
        }

        [Fact]
        public void SalesCollectedGap_SubtractsCollectedFromSalesOverTheClosedBills()
        {
            var bills = new[]
            {
                new DashboardLeakageBillRecord(Guid.NewGuid(), 80m, 30m, From.AddDays(1)),
                new DashboardLeakageBillRecord(Guid.NewGuid(), 60m, 60m, From.AddDays(2)),
                new DashboardLeakageBillRecord(Guid.NewGuid(), 20m, 10m, From.AddDays(3)),
            };

            var signal = DashboardLeakageRules.SalesCollectedGap(bills, 1);

            Assert.Equal(60m, signal.Value);
            Assert.Equal(2, signal.Count);
        }

        [Fact]
        public void Contributors_AreCappedAtFiftyPerPageAndPaged()
        {
            var records = Enumerable.Range(0, 120)
                .Select(index => new DashboardLeakageRecord(
                    Guid.NewGuid(),
                    index + 1,
                    From.AddMinutes(index)))
                .ToList();

            var first = DashboardLeakageRules.RejectedAndExpiredOrders(records, 1);
            var third = DashboardLeakageRules.RejectedAndExpiredOrders(records, 3);

            Assert.Equal(120, first.Count);
            Assert.Equal(50, first.Contributors.Count);
            Assert.True(first.HasMoreContributors);
            Assert.Equal(120m, first.Contributors[0].Total);
            Assert.Equal(20, third.Contributors.Count);
            Assert.False(third.HasMoreContributors);
            Assert.Equal(20m, third.Contributors[0].Total);

            var beyond = DashboardLeakageRules.RejectedAndExpiredOrders(records, int.MaxValue);
            Assert.Empty(beyond.Contributors);
            Assert.False(beyond.HasMoreContributors);
        }

        [Fact]
        public async Task SignalNoticeAndDefinitions_DescribeEverySignalWithoutClaimingALoss()
        {
            var store = new FakeLeakageStore(EmptySnapshot());

            var result = await Handler(store).Handle(Query(), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Contains("not a verified loss", result.Value.Notice, StringComparison.Ordinal);
            Assert.Equal(
                DashboardLeakageKeys.All.OrderBy(key => key).ToArray(),
                result.Value.Definitions.Select(definition => definition.Key).OrderBy(key => key).ToArray());
            Assert.Equal(
                DashboardLeakageKeys.All.OrderBy(key => key).ToArray(),
                result.Value.Signals.Select(signal => signal.Key).OrderBy(key => key).ToArray());
            Assert.All(result.Value.Definitions, definition =>
            {
                Assert.False(string.IsNullOrWhiteSpace(definition.Label));
                Assert.False(string.IsNullOrWhiteSpace(definition.Formula));
                Assert.NotEmpty(definition.Includes);
                Assert.NotEmpty(definition.Excludes);
                Assert.DoesNotContain("profit", definition.Label, StringComparison.OrdinalIgnoreCase);
            });
            Assert.Contains(
                "gap",
                Assert.Single(
                    result.Value.Definitions,
                    definition => definition.Key == DashboardLeakageKeys.SalesCollectedGap).Label,
                StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task StaffBranch_AlwaysReplacesTheRequestedBranch()
        {
            var store = new FakeLeakageStore(EmptySnapshot());
            var staff = new TenantAccountAccess(true, "1", true) { BranchId = BranchB };
            var handler = new GetDashboardLeakageQueryHandler(
                store,
                new FakeSettingsReader("UTC"),
                new FakeCurrentTenantUser(staff));

            await handler.Handle(Query(), CancellationToken.None);

            Assert.Equal(BranchB, store.LastRequest!.BranchId);
        }

        [Fact]
        public async Task OwnerWithoutBranch_ReportsEveryBranch()
        {
            var store = new FakeLeakageStore(EmptySnapshot());
            var handler = new GetDashboardLeakageQueryHandler(
                store,
                new FakeSettingsReader("UTC"),
                new FakeCurrentTenantUser(new TenantAccountAccess(false, "1", false)));

            await handler.Handle(Query(), CancellationToken.None);

            Assert.Null(store.LastRequest!.BranchId);
        }

        [Fact]
        public void Validator_RejectsAnInvalidRangeAndAPageBelowOne()
        {
            var validator = new GetDashboardLeakageQueryValidator();

            validator.TestValidate(new GetDashboardLeakageQuery())
                .ShouldHaveValidationErrorFor(query => query.From);
            validator.TestValidate(new GetDashboardLeakageQuery
            {
                From = new DateOnly(2026, 11, 2),
                To = new DateOnly(2026, 11, 1),
            }).ShouldHaveValidationErrorFor(query => query.To);
            validator.TestValidate(Query() with { ContributorPage = 0 })
                .ShouldHaveValidationErrorFor(query => query.ContributorPage);
            validator.TestValidate(Query()).ShouldNotHaveAnyValidationErrors();
            validator.TestValidate(Query() with { BranchId = Guid.Empty })
                .ShouldHaveValidationErrorFor(query => query.BranchId);
        }

        private static void AssertSignal(
            IReadOnlyList<DashboardLeakageSignal> signals,
            string key,
            int count,
            decimal value)
        {
            var signal = Assert.Single(signals, candidate => candidate.Key == key);
            Assert.Equal(count, signal.Count);
            Assert.Equal(value, signal.Value);
            Assert.Equal(1, signal.ContributorPage);
            Assert.Equal(count, signal.Contributors.Count);
        }

        private static GetDashboardLeakageQuery Query() => new()
        {
            From = new DateOnly(2026, 11, 1),
            To = new DateOnly(2026, 12, 1),
        };

        private static GetDashboardLeakageQueryHandler Handler(FakeLeakageStore store) =>
            new(
                store,
                new FakeSettingsReader("UTC"),
                new FakeCurrentTenantUser(new TenantAccountAccess(false, "1", false)));

        private static DashboardLeakageSnapshot EmptySnapshot() =>
            new([], [], []);

        private static Order AcceptedOrder(Guid branchId, decimal amount, DateTimeOffset acceptedAt)
        {
            var order = NewOrder(branchId, amount, acceptedAt, OrderServiceType.DineIn);
            order.Accept(ActorId, acceptedAt);
            return order;
        }

        private static Order RejectedOrder(Guid branchId, decimal amount, DateTimeOffset decidedAt)
        {
            var order = NewOrder(branchId, amount, decidedAt.AddMinutes(-10), OrderServiceType.DineIn);
            order.Reject(ActorId, "Out of stock", decidedAt);
            return order;
        }

        private static Order ExpiredOrder(Guid branchId, decimal amount, DateTimeOffset expiredAt)
        {
            var order = NewOrder(branchId, amount, expiredAt.AddMinutes(-10), OrderServiceType.DineIn);
            order.Expire(expiredAt);
            return order;
        }

        private static Order NewOrder(
            Guid branchId,
            decimal amount,
            DateTimeOffset submittedAt,
            OrderServiceType serviceType)
        {
            var item = OrderItem.Create(
                Guid.NewGuid(),
                1,
                null,
                null,
                null,
                "Coffee",
                "Large",
                amount,
                1,
                [],
                submittedAt,
                ActorId);

            return Order.Create(
                Guid.NewGuid(),
                branchId,
                Guid.NewGuid(),
                1,
                new DateOnly(submittedAt.Year, submittedAt.Month, submittedAt.Day),
                serviceType,
                OrderSource.Captain,
                "Guest",
                null,
                ActorId,
                "Abcdefghijklmnopqrstuv",
                Guid.NewGuid().ToString("N"),
                null,
                submittedAt,
                submittedAt.AddMinutes(5),
                [item],
                ActorId);
        }

        private static Bill ClosedBill(Guid branchId, decimal amount, DateTimeOffset closedAt)
        {
            var bill = OpenBill(branchId, closedAt.AddMinutes(-30));
            bill.Close("Guest left", ActorId, closedAt);
            return bill;
        }

        private static Bill OpenBill(Guid branchId, DateTimeOffset openedAt) =>
            Bill.Create(
                Guid.NewGuid(),
                branchId,
                Guid.NewGuid(),
                "Abcdefghijklmnopqrstuv",
                openedAt,
                ActorId);

        private static Payment RejectedProof(decimal amount, DateTimeOffset rejectedAt)
        {
            var payment = PendingProof(amount, rejectedAt.AddMinutes(-5));
            payment.RejectReview("Proof does not match", ActorId, rejectedAt);
            return payment;
        }

        private static Payment ConfirmedProof(decimal amount, DateTimeOffset confirmedAt)
        {
            var payment = PendingProof(amount, confirmedAt.AddMinutes(-5));
            payment.ConfirmReview(amount, ActorId, confirmedAt);
            return payment;
        }

        private static Payment PendingProof(decimal amount, DateTimeOffset submittedAt) =>
            Payment.SubmitOnlineProof(
                Guid.NewGuid(),
                Guid.NewGuid(),
                amount,
                "media/reference",
                null,
                Guid.NewGuid().ToString("N"),
                submittedAt);

        private sealed class FakeLeakageStore(DashboardLeakageSnapshot snapshot) : IDashboardLeakageStore
        {
            public DashboardLeakageRequest? LastRequest { get; private set; }

            public Task<DashboardLeakageSnapshot> GetAsync(
                DashboardLeakageRequest request,
                CancellationToken cancellationToken)
            {
                LastRequest = request;
                return Task.FromResult(snapshot);
            }
        }

        private sealed class FakeSettingsReader(string timezone) : ICafeSettingsReader
        {
            public Task<CafeProfileResponse?> GetAsync(CancellationToken cancellationToken) =>
                Task.FromResult<CafeProfileResponse?>(new CafeProfileResponse
                {
                    CafeName = "Cafe",
                    LogoMediaReference = null,
                    PrimaryColor = null,
                    SecondaryColor = null,
                    AccentColor = null,
                    ContactPhone = null,
                    ContactEmail = null,
                    ContactAddress = null,
                    Timezone = timezone,
                    Currency = "egp",
                    ConcurrencyToken = "1",
                });
        }

        private sealed class FakeCurrentTenantUser(TenantAccountAccess? access) : ICurrentTenantUser
        {
            public bool IsAuthenticated => true;

            public Guid? UserId => ActorId;

            public Guid? TenantId => Guid.NewGuid();

            public IReadOnlyCollection<string> Roles => ["Owner"];

            public Guid? BranchId => access?.BranchId;

            public TenantAccountAccess? AccountAccess => access;
        }
    }
}
