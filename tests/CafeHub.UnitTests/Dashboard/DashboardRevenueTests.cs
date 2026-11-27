using System.Threading;
using CafeHub.Application.CafeProfile.Contracts;
using CafeHub.Application.CafeProfile.DTOs;
using CafeHub.Application.Dashboard;
using CafeHub.Application.Dashboard.Contracts;
using CafeHub.Application.Dashboard.DTOs;
using CafeHub.Application.Dashboard.Queries;
using CafeHub.Application.Identity;
using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Dashboard
{
    public sealed class DashboardRevenueTests
    {
        private static readonly Guid BranchA = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid BranchB = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid ActorId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly DateTimeOffset From = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        private static readonly DateTimeOffset To = new(2026, 12, 1, 0, 0, 0, TimeSpan.Zero);

        [Fact]
        public void AcceptedOrdersInRange_ExcludeRejectedExpiredPendingAndOutOfRange()
        {
            var atFrom = AcceptedOrder(BranchA, 10m, From);
            var inside = AcceptedOrder(BranchA, 20m, From.AddDays(3));
            var atTo = AcceptedOrder(BranchA, 30m, To);
            var before = AcceptedOrder(BranchA, 40m, From.AddMinutes(-1));
            var rejected = RejectedOrder(BranchA, 50m, From.AddDays(1));
            var expired = ExpiredOrder(BranchA, 60m, From.AddDays(1));
            var pending = PendingOrder(BranchA, 70m, From.AddDays(1));
            var orders = new[] { atFrom, inside, atTo, before, rejected, expired, pending }.AsQueryable();

            var selected = orders.WhereAcceptedInRange(new DashboardRevenueRequest(null, From, To)).ToList();

            Assert.Equal(new[] { atFrom.Id, inside.Id }, selected.Select(order => order.Id).ToArray());
        }

        [Fact]
        public void AcceptedOrdersInRange_ApplyTheBranchFilterOnlyWhenOneIsRequested()
        {
            var branchA = AcceptedOrder(BranchA, 10m, From.AddDays(1));
            var branchB = AcceptedOrder(BranchB, 20m, From.AddDays(1));
            var orders = new[] { branchA, branchB }.AsQueryable();

            var all = orders.WhereAcceptedInRange(new DashboardRevenueRequest(null, From, To)).ToList();
            var onlyB = orders.WhereAcceptedInRange(new DashboardRevenueRequest(BranchB, From, To)).ToList();

            Assert.Equal(2, all.Count);
            Assert.Equal(new[] { branchB.Id }, onlyB.Select(order => order.Id).ToArray());
        }

        [Fact]
        public void ConfirmedPaymentsInRange_ExcludePendingReviewRejectedAndOutOfRange()
        {
            var cash = ConfirmedCashPayment(15.5m, From.AddDays(2));
            var online = ConfirmedOnlinePayment(24.5m, From.AddDays(5));
            var pending = PendingOnlinePayment(30m, From.AddDays(3));
            var rejected = RejectedOnlinePayment(40m, From.AddDays(3));
            var atTo = ConfirmedCashPayment(50m, To);
            var payments = new[] { cash, online, pending, rejected, atTo }.AsQueryable();

            var selected = payments.WhereConfirmedInRange(new DashboardRevenueRequest(null, From, To)).ToList();

            Assert.Equal(new[] { cash.Id, online.Id }, selected.Select(payment => payment.Id).ToArray());
        }

        [Fact]
        public void BillableOrders_ExcludeWifiOnlyOrdersFromTheAverageDenominator()
        {
            var dineIn = AcceptedOrder(BranchA, 10m, From, OrderServiceType.DineIn);
            var takeaway = AcceptedOrder(BranchA, 20m, From, OrderServiceType.Takeaway);
            var wifiOnly = WifiOnlyOrder(BranchA, 5m, From);
            var orders = new[] { dineIn, takeaway, wifiOnly }.AsQueryable();

            var billable = orders.WhereBillableOrders().ToList();

            Assert.Equal(new[] { dineIn.Id, takeaway.Id }, billable.Select(order => order.Id).ToArray());
        }

        [Fact]
        public async Task ControlledDataset_ReturnsTheExactRevenueMetrics()
        {
            var store = new FakeRevenueStore(new DashboardRevenueSnapshot(
                Sales: 120.50m,
                Collected: 100m,
                Orders: 4,
                NonWifiOrders: 3,
                CashCollected: 60m,
                OnlineCollected: 40m));

            var result = await Handler(store).Handle(
                new GetDashboardRevenueQuery
                {
                    From = new DateOnly(2026, 11, 1),
                    To = new DateOnly(2026, 12, 1),
                },
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            var metrics = result.Value.Metrics;
            Assert.Equal(120.50m, metrics.Sales);
            Assert.Equal(100m, metrics.Collected);
            Assert.Equal(4, metrics.Orders);
            Assert.Equal(40.17m, metrics.AverageOrderValue);
            Assert.Equal(new DashboardPaymentSplit(60m, 40m), metrics.PaymentSplit);
        }

        [Fact]
        public async Task NoBillableOrder_ReturnsNullAverageOrderValue()
        {
            var store = new FakeRevenueStore(new DashboardRevenueSnapshot(0m, 0m, 2, 0, 0m, 0m));

            var result = await Handler(store).Handle(
                new GetDashboardRevenueQuery
                {
                    From = new DateOnly(2026, 11, 1),
                    To = new DateOnly(2026, 12, 1),
                },
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Null(result.Value.Metrics.AverageOrderValue);
            Assert.Equal(2, result.Value.Metrics.Orders);
        }

        [Fact]
        public async Task LocalRange_SendsCafeLocalMidnightsToTheStore()
        {
            var store = new FakeRevenueStore(new DashboardRevenueSnapshot(0m, 0m, 0, 0, 0m, 0m));

            var result = await Handler(store).Handle(
                new GetDashboardRevenueQuery
                {
                    From = new DateOnly(2026, 11, 26),
                    To = new DateOnly(2026, 11, 27),
                },
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(new DateTimeOffset(2026, 11, 26, 0, 0, 0, TimeSpan.Zero), store.LastRequest!.From);
            Assert.Equal(new DateTimeOffset(2026, 11, 27, 0, 0, 0, TimeSpan.Zero), store.LastRequest.To);
        }

        [Fact]
        public void DateRange_UsesEachBoundarysCafeLocalOffset()
        {
            var newYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

            var range = DashboardDateRange.Resolve(
                new DateOnly(2026, 3, 8),
                new DateOnly(2026, 3, 9),
                newYork);

            Assert.True(range.IsSuccess);
            Assert.Equal(new DateTimeOffset(2026, 3, 8, 5, 0, 0, TimeSpan.Zero), range.Value.From);
            Assert.Equal(new DateTimeOffset(2026, 3, 9, 4, 0, 0, TimeSpan.Zero), range.Value.To);
        }

        [Fact]
        public void DateRange_RejectsAReversedRange()
        {
            var range = DashboardDateRange.Resolve(
                new DateOnly(2026, 1, 2),
                new DateOnly(2026, 1, 2),
                TimeZoneInfo.Utc);

            Assert.True(range.IsFailure);
            Assert.Equal(DashboardErrors.InvalidRangeCode, range.Error!.Code);
        }

        [Fact]
        public void DateRange_RejectsARangeLongerThanTheMaximum()
        {
            var from = new DateOnly(2026, 1, 1);

            var range = DashboardDateRange.Resolve(
                from,
                from.AddDays(DashboardDateRange.MaximumDays + 1),
                TimeZoneInfo.Utc);

            Assert.True(range.IsFailure);
            Assert.Equal(DashboardErrors.InvalidRangeCode, range.Error!.Code);
        }

        [Fact]
        public void DateRange_AcceptsTheMaximumRange()
        {
            var from = new DateOnly(2026, 1, 1);

            var range = DashboardDateRange.Resolve(
                from,
                from.AddDays(DashboardDateRange.MaximumDays),
                TimeZoneInfo.Utc);

            Assert.True(range.IsSuccess);
        }

        [Fact]
        public async Task StaffBranch_AlwaysReplacesTheRequestedBranch()
        {
            var store = new FakeRevenueStore(new DashboardRevenueSnapshot(0m, 0m, 0, 0, 0m, 0m));
            var staff = new TenantAccountAccess(true, "1", true) { BranchId = BranchB };
            var handler = new GetDashboardRevenueQueryHandler(
                store,
                new FakeSettingsReader("UTC"),
                new FakeCurrentTenantUser(staff));

            await handler.Handle(
                new GetDashboardRevenueQuery
                {
                    From = new DateOnly(2026, 11, 1),
                    To = new DateOnly(2026, 11, 2),
                    BranchId = BranchA,
                },
                CancellationToken.None);

            Assert.Equal(BranchB, store.LastRequest!.BranchId);
        }

        [Fact]
        public async Task OwnerWithoutBranch_ReportsEveryBranch()
        {
            var store = new FakeRevenueStore(new DashboardRevenueSnapshot(0m, 0m, 0, 0, 0m, 0m));
            var handler = new GetDashboardRevenueQueryHandler(
                store,
                new FakeSettingsReader("UTC"),
                new FakeCurrentTenantUser(new TenantAccountAccess(false, "1", false)));

            await handler.Handle(
                new GetDashboardRevenueQuery
                {
                    From = new DateOnly(2026, 11, 1),
                    To = new DateOnly(2026, 11, 2),
                },
                CancellationToken.None);

            Assert.Null(store.LastRequest!.BranchId);
        }

        [Fact]
        public async Task InvalidRange_ReturnsInvalidRangeWithoutReadingTheStore()
        {
            var store = new FakeRevenueStore(new DashboardRevenueSnapshot(0m, 0m, 0, 0, 0m, 0m));

            var result = await Handler(store).Handle(
                new GetDashboardRevenueQuery
                {
                    From = new DateOnly(2026, 11, 2),
                    To = new DateOnly(2026, 11, 1),
                },
                CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(DashboardErrors.InvalidRangeCode, result.Error!.Code);
            Assert.Null(store.LastRequest);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Not/AZone")]
        public async Task MissingOrUnknownTimeZone_FailsClosed(string? timeZone)
        {
            var store = new FakeRevenueStore(new DashboardRevenueSnapshot(0m, 0m, 0, 0, 0m, 0m));
            var handler = new GetDashboardRevenueQueryHandler(
                store,
                new FakeSettingsReader(timeZone!),
                new FakeCurrentTenantUser(new TenantAccountAccess(false, "1", false)));

            var result = await handler.Handle(
                new GetDashboardRevenueQuery
                {
                    From = new DateOnly(2026, 11, 1),
                    To = new DateOnly(2026, 11, 2),
                },
                CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(DashboardErrors.TimeZoneUnavailableCode, result.Error!.Code);
            Assert.Null(store.LastRequest);
        }

        [Fact]
        public async Task Response_IncludesADefinitionForEveryReturnedMetricKey()
        {
            var store = new FakeRevenueStore(new DashboardRevenueSnapshot(0m, 0m, 0, 0, 0m, 0m));

            var result = await Handler(store).Handle(
                new GetDashboardRevenueQuery
                {
                    From = new DateOnly(2026, 11, 1),
                    To = new DateOnly(2026, 11, 2),
                },
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(
                DashboardMetricKeys.All.OrderBy(key => key).ToArray(),
                result.Value.MetricDefinitions.Select(definition => definition.Key).OrderBy(key => key).ToArray());
            Assert.All(result.Value.MetricDefinitions, definition =>
            {
                Assert.False(string.IsNullOrWhiteSpace(definition.Label));
                Assert.False(string.IsNullOrWhiteSpace(definition.Formula));
                Assert.NotEmpty(definition.Includes);
                Assert.NotEmpty(definition.Excludes);
            });
        }

        [Fact]
        public void Catalog_NeverClaimsNetProfitOrMargin()
        {
            Assert.DoesNotContain(DashboardMetricKeys.All, key =>
                key.Contains("profit", StringComparison.OrdinalIgnoreCase)
                || key.Contains("margin", StringComparison.OrdinalIgnoreCase));
            Assert.All(DashboardMetricCatalog.All, definition =>
            {
                Assert.DoesNotContain("profit", definition.Label, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("margin", definition.Label, StringComparison.OrdinalIgnoreCase);
            });

            Assert.Equal("Sales", Assert.Single(
                DashboardMetricCatalog.All,
                definition => definition.Key == DashboardMetricKeys.Sales).Label);
            Assert.Equal("Collected", Assert.Single(
                DashboardMetricCatalog.All,
                definition => definition.Key == DashboardMetricKeys.Collected).Label);
        }

        private static GetDashboardRevenueQueryHandler Handler(FakeRevenueStore store) =>
            new(store, new FakeSettingsReader("UTC"), new FakeCurrentTenantUser(new TenantAccountAccess(false, "1", false)));

        private static Order AcceptedOrder(
            Guid branchId,
            decimal amount,
            DateTimeOffset acceptedAt,
            OrderServiceType serviceType = OrderServiceType.DineIn)
        {
            var order = NewOrder(branchId, amount, acceptedAt, serviceType);
            order.Accept(ActorId, acceptedAt);
            return order;
        }

        private static Order RejectedOrder(Guid branchId, decimal amount, DateTimeOffset decidedAt)
        {
            var order = NewOrder(branchId, amount, decidedAt, OrderServiceType.DineIn);
            order.Reject(ActorId, "Out of stock", decidedAt);
            return order;
        }

        private static Order ExpiredOrder(Guid branchId, decimal amount, DateTimeOffset expiredAt)
        {
            var order = NewOrder(branchId, amount, expiredAt, OrderServiceType.DineIn);
            order.Expire(expiredAt.AddMinutes(10));
            return order;
        }

        private static Order PendingOrder(Guid branchId, decimal amount, DateTimeOffset submittedAt) =>
            NewOrder(branchId, amount, submittedAt, OrderServiceType.DineIn);

        private static Order WifiOnlyOrder(Guid branchId, decimal amount, DateTimeOffset acceptedAt)
        {
            var order = NewOrder(branchId, amount, acceptedAt, OrderServiceType.WifiOnly);
            order.Accept(ActorId, acceptedAt);
            return order;
        }

        private static Order NewOrder(
            Guid branchId,
            decimal amount,
            DateTimeOffset submittedAt,
            OrderServiceType serviceType)
        {
            var tableId = serviceType == OrderServiceType.WifiOnly ? (Guid?)null : Guid.NewGuid();
            var item = serviceType == OrderServiceType.WifiOnly
                ? OrderItem.CreateWifiLine(
                    Guid.NewGuid(),
                    1,
                    Guid.NewGuid(),
                    "30 minutes",
                    30,
                    10m,
                    amount,
                    1,
                    submittedAt,
                    ActorId)
                : OrderItem.Create(
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
                tableId,
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

        private static Payment ConfirmedCashPayment(decimal amount, DateTimeOffset confirmedAt) =>
            Payment.ConfirmCash(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                amount,
                ActorId,
                Guid.NewGuid().ToString("N"),
                confirmedAt,
                ActorId);

        private static Payment ConfirmedOnlinePayment(decimal amount, DateTimeOffset confirmedAt)
        {
            var payment = PendingOnlinePayment(amount, confirmedAt.AddMinutes(-5));
            payment.ConfirmReview(amount, ActorId, confirmedAt);
            return payment;
        }

        private static Payment PendingOnlinePayment(decimal amount, DateTimeOffset submittedAt) =>
            Payment.SubmitOnlineProof(
                Guid.NewGuid(),
                Guid.NewGuid(),
                amount,
                "media/reference",
                null,
                Guid.NewGuid().ToString("N"),
                submittedAt);

        private static Payment RejectedOnlinePayment(decimal amount, DateTimeOffset rejectedAt)
        {
            var payment = PendingOnlinePayment(amount, rejectedAt.AddMinutes(-5));
            payment.RejectReview("Proof does not match", ActorId, rejectedAt);
            return payment;
        }

        private sealed class FakeRevenueStore(DashboardRevenueSnapshot snapshot) : IDashboardRevenueStore
        {
            public DashboardRevenueRequest? LastRequest { get; private set; }

            public Task<DashboardRevenueSnapshot> GetAsync(
                DashboardRevenueRequest request,
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
