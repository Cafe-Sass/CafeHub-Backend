using System.Threading;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.CafeProfile.Contracts;
using CafeHub.Application.CafeProfile.DTOs;
using CafeHub.Application.Dashboard;
using CafeHub.Application.Dashboard.Contracts;
using CafeHub.Application.Dashboard.DTOs;
using CafeHub.Application.Dashboard.Queries;
using CafeHub.Application.Identity;
using CafeHub.Application.Wifi.Contracts;
using CafeHub.Application.Wifi.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Dashboard
{
    public sealed class DashboardSalesInsightsTests
    {
        private static readonly Guid BranchA = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid BranchB = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid ActorId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        [Fact]
        public void BestSellersByQuantity_RankDescendingAndBreakTiesByNameThenId()
        {
            var lowId = Guid.Parse("00000000-0000-0000-0000-000000000001");
            var highId = Guid.Parse("00000000-0000-0000-0000-000000000002");
            var rows = new[]
            {
                Row(Guid.NewGuid(), "Latte", 5, 50m),
                Row(highId, "Espresso", 5, 25m),
                Row(lowId, "Espresso", 5, 25m),
                Row(Guid.NewGuid(), "Americano", 5, 20m),
                Row(Guid.NewGuid(), "Mocha", 1, 10m),
            };

            var ranked = DashboardInsightsRules.RankBestSellersByQuantity(rows);

            Assert.Equal(
                new[] { "Americano", "Espresso", "Espresso", "Latte", "Mocha" },
                ranked.Select(seller => seller.Name).ToArray());
            Assert.Equal(lowId, ranked[1].ProductId);
            Assert.Equal(highId, ranked[2].ProductId);
        }

        [Fact]
        public void BestSellersBySales_RankDescendingAndBreakTiesByNameThenId()
        {
            var lowId = Guid.Parse("00000000-0000-0000-0000-000000000001");
            var highId = Guid.Parse("00000000-0000-0000-0000-000000000002");
            var rows = new[]
            {
                Row(Guid.NewGuid(), "Tea", 9, 90m),
                Row(highId, "Cake", 4, 90m),
                Row(lowId, "Cake", 4, 90m),
                Row(Guid.NewGuid(), "Juice", 1, 5m),
            };

            var ranked = DashboardInsightsRules.RankBestSellersBySales(rows);

            Assert.Equal(
                new[] { "Cake", "Cake", "Tea", "Juice" },
                ranked.Select(seller => seller.Name).ToArray());
            Assert.Equal(lowId, ranked[0].ProductId);
            Assert.Equal(highId, ranked[1].ProductId);
        }

        [Fact]
        public void BestSellers_MergeRenamedSnapshotsOfTheSameProduct()
        {
            var productId = Guid.NewGuid();
            var rows = new[]
            {
                Row(productId, "Flat White", 3, 45m),
                Row(productId, "Coffee", 7, 70m),
            };

            var ranked = DashboardInsightsRules.RankBestSellersByQuantity(rows);

            var seller = Assert.Single(ranked);
            Assert.Equal(productId, seller.ProductId);
            Assert.Equal("Coffee", seller.Name);
            Assert.Equal(10, seller.Quantity);
            Assert.Equal(115m, seller.Sales);
        }

        [Fact]
        public void BestSellers_AreCappedAtTwenty()
        {
            var rows = Enumerable.Range(1, 25)
                .Select(index => Row(Guid.NewGuid(), $"Product {index:00}", index, index))
                .ToList();

            var ranked = DashboardInsightsRules.RankBestSellersByQuantity(rows);

            Assert.Equal(DashboardInsightsRules.MaximumBestSellers, ranked.Count);
            Assert.Equal(25, ranked[0].Quantity);
            Assert.Equal(6, ranked[^1].Quantity);
        }

        [Fact]
        public void PeakHours_GroupAcceptedOrdersByCafeLocalHour()
        {
            var tokyo = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");
            var times = new[]
            {
                new DateTimeOffset(2026, 11, 1, 22, 30, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 11, 1, 22, 45, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 11, 2, 1, 0, 0, TimeSpan.Zero),
            };

            var hours = DashboardInsightsRules.GroupPeakHours(times, tokyo);

            Assert.Equal(24, hours.Count);
            Assert.Equal(2, hours.Single(hour => hour.Hour == 7).Orders);
            Assert.Equal(1, hours.Single(hour => hour.Hour == 10).Orders);
            Assert.Equal(0, hours.Single(hour => hour.Hour == 0).Orders);
        }

        [Fact]
        public void FreePolicy_ReturnsNullAttachRateWithReason()
        {
            var rate = DashboardInsightsRules.ResolveWifiAttachRate(WifiPolicy.Free, 10, 5);

            Assert.Null(rate.Rate);
            Assert.Equal(DashboardInsightsRules.WifiPolicyFreeReason, rate.Reason);
        }

        [Fact]
        public void PaidPolicy_ComputesTheAttachRateFromDineInOrders()
        {
            var rate = DashboardInsightsRules.ResolveWifiAttachRate(WifiPolicy.Paid, 3, 1);

            Assert.Equal(0.3333m, rate.Rate);
            Assert.Null(rate.Reason);
        }

        [Fact]
        public void PaidPolicyWithoutDineInOrders_ReturnsNullWithReason()
        {
            var rate = DashboardInsightsRules.ResolveWifiAttachRate(WifiPolicy.Paid, 0, 0);

            Assert.Null(rate.Rate);
            Assert.Equal(DashboardInsightsRules.NoDineInOrdersReason, rate.Reason);
        }

        [Fact]
        public async Task ControlledDataset_ReturnsEverySalesInsight()
        {
            var latteId = Guid.NewGuid();
            var itemId = Guid.NewGuid();
            var batchId = Guid.NewGuid();
            var snapshot = new DashboardInsightsSnapshot(
                [
                    new DashboardProductSalesRow(latteId, "Latte", 4, 60m),
                    new DashboardProductSalesRow(Guid.NewGuid(), "Tea", 9, 45m),
                ],
                [new DateTimeOffset(2026, 11, 10, 9, 15, 0, TimeSpan.Zero)],
                4,
                1,
                [new DashboardNearExpiryRow(itemId, "Milk", BranchA, batchId, new DateOnly(2026, 11, 14))]);
            var store = new FakeInsightsStore(snapshot);

            var result = await Handler(store, WifiPolicy.Paid).Handle(Query(), CancellationToken.None);

            Assert.True(result.IsSuccess);
            var metrics = result.Value.Metrics;
            Assert.Equal(
                new[] { "Tea", "Latte" },
                metrics.BestSellersByQuantity.Select(seller => seller.Name).ToArray());
            Assert.Equal(
                new[] { "Latte", "Tea" },
                metrics.BestSellersBySales.Select(seller => seller.Name).ToArray());
            Assert.Equal(1, metrics.PeakHours.Single(hour => hour.Hour == 9).Orders);
            Assert.Equal(0.25m, metrics.WifiAttachRate.Rate);
            var nearExpiry = Assert.Single(metrics.NearExpiryItems);
            Assert.Equal(itemId, nearExpiry.ItemId);
            Assert.Equal("Milk", nearExpiry.ItemName);
            Assert.Equal(BranchA, nearExpiry.BranchId);
            Assert.Equal(batchId, nearExpiry.BatchId);
            Assert.Equal(new DateOnly(2026, 11, 14), nearExpiry.ExpiryDate);
        }

        [Fact]
        public async Task FreePolicy_ReturnsANullAttachRateInTheResponse()
        {
            var snapshot = new DashboardInsightsSnapshot([], [], 4, 1, []);
            var store = new FakeInsightsStore(snapshot);

            var result = await Handler(store, WifiPolicy.Free).Handle(Query(), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Null(result.Value.Metrics.WifiAttachRate.Rate);
            Assert.Equal(
                DashboardInsightsRules.WifiPolicyFreeReason,
                result.Value.Metrics.WifiAttachRate.Reason);
        }

        [Fact]
        public async Task StaffBranch_AlwaysReplacesTheRequestedBranch()
        {
            var store = new FakeInsightsStore(EmptySnapshot());
            var staff = new TenantAccountAccess(true, "1", true) { BranchId = BranchB };
            var handler = new GetDashboardSalesInsightsQueryHandler(
                store,
                new FakeWifiSettingsStore(WifiPolicy.Paid),
                new FakeSettingsReader("UTC"),
                new FakeCurrentTenantUser(staff));

            await handler.Handle(
                new GetDashboardSalesInsightsQuery
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
            var store = new FakeInsightsStore(EmptySnapshot());

            await Handler(store, WifiPolicy.Paid).Handle(
                new GetDashboardSalesInsightsQuery
                {
                    From = new DateOnly(2026, 11, 1),
                    To = new DateOnly(2026, 11, 2),
                },
                CancellationToken.None);

            Assert.Null(store.LastRequest!.BranchId);
        }

        [Fact]
        public async Task LocalRange_SendsCafeLocalMidnightsToTheStore()
        {
            var store = new FakeInsightsStore(EmptySnapshot());

            await Handler(store, WifiPolicy.Paid).Handle(
                new GetDashboardSalesInsightsQuery
                {
                    From = new DateOnly(2026, 11, 26),
                    To = new DateOnly(2026, 11, 27),
                },
                CancellationToken.None);

            Assert.Equal(new DateTimeOffset(2026, 11, 26, 0, 0, 0, TimeSpan.Zero), store.LastRequest!.From);
            Assert.Equal(new DateTimeOffset(2026, 11, 27, 0, 0, 0, TimeSpan.Zero), store.LastRequest.To);
        }

        [Fact]
        public async Task NearExpiryItems_AreOrderedByExpiryThenItemName()
        {
            var later = new DashboardNearExpiryRow(
                Guid.NewGuid(), "Cream", BranchA, Guid.NewGuid(), new DateOnly(2026, 11, 20));
            var soonerYogurt = new DashboardNearExpiryRow(
                Guid.NewGuid(), "Yogurt", BranchA, Guid.NewGuid(), new DateOnly(2026, 11, 12));
            var soonerBeans = new DashboardNearExpiryRow(
                Guid.NewGuid(), "Beans", BranchA, Guid.NewGuid(), new DateOnly(2026, 11, 12));
            var store = new FakeInsightsStore(
                EmptySnapshot() with { NearExpiryItems = [later, soonerYogurt, soonerBeans] });

            var result = await Handler(store, WifiPolicy.Paid).Handle(Query(), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(
                new[] { "Beans", "Yogurt", "Cream" },
                result.Value.Metrics.NearExpiryItems.Select(item => item.ItemName).ToArray());
        }

        [Fact]
        public async Task InvalidRange_ReturnsInvalidRangeWithoutReadingTheStore()
        {
            var store = new FakeInsightsStore(EmptySnapshot());

            var result = await Handler(store, WifiPolicy.Paid).Handle(
                new GetDashboardSalesInsightsQuery
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
            var store = new FakeInsightsStore(EmptySnapshot());
            var handler = new GetDashboardSalesInsightsQueryHandler(
                store,
                new FakeWifiSettingsStore(WifiPolicy.Paid),
                new FakeSettingsReader(timeZone!),
                new FakeCurrentTenantUser(new TenantAccountAccess(false, "1", false)));

            var result = await handler.Handle(Query(), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(DashboardErrors.TimeZoneUnavailableCode, result.Error!.Code);
            Assert.Null(store.LastRequest);
        }

        [Fact]
        public async Task Response_IncludesADefinitionForEverySalesInsightMetricKey()
        {
            var result = await Handler(new FakeInsightsStore(EmptySnapshot()), WifiPolicy.Paid)
                .Handle(Query(), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(
                DashboardMetricKeys.All.OrderBy(key => key).ToArray(),
                result.Value.MetricDefinitions.Select(definition => definition.Key).OrderBy(key => key).ToArray());
            Assert.Contains(result.Value.MetricDefinitions, definition => definition.Key == DashboardMetricKeys.BestSellers);
            Assert.Contains(result.Value.MetricDefinitions, definition => definition.Key == DashboardMetricKeys.PeakHours);
            Assert.Contains(result.Value.MetricDefinitions, definition => definition.Key == DashboardMetricKeys.WifiAttachRate);
            Assert.Contains(result.Value.MetricDefinitions, definition => definition.Key == DashboardMetricKeys.NearExpiryItems);
        }

        private static GetDashboardSalesInsightsQueryHandler Handler(FakeInsightsStore store, WifiPolicy policy) =>
            new(
                store,
                new FakeWifiSettingsStore(policy),
                new FakeSettingsReader("UTC"),
                new FakeCurrentTenantUser(new TenantAccountAccess(false, "1", false)));

        private static GetDashboardSalesInsightsQuery Query() =>
            new()
            {
                From = new DateOnly(2026, 11, 1),
                To = new DateOnly(2026, 12, 1),
            };

        private static DashboardInsightsSnapshot EmptySnapshot() =>
            new([], [], 0, 0, []);

        private static DashboardProductSalesRow Row(
            Guid productId,
            string productName,
            int quantity,
            decimal sales) =>
            new(productId, productName, quantity, sales);

        private sealed class FakeInsightsStore(DashboardInsightsSnapshot snapshot) : IDashboardInsightsStore
        {
            public DashboardInsightsRequest? LastRequest { get; private set; }

            public Task<DashboardInsightsSnapshot> GetAsync(
                DashboardInsightsRequest request,
                CancellationToken cancellationToken)
            {
                LastRequest = request;
                return Task.FromResult(snapshot);
            }
        }

        private sealed class FakeWifiSettingsStore(WifiPolicy policy) : IWifiSettingsStore
        {
            public Task<WifiSettingsResponse> GetAsync(Guid? branchId, CancellationToken cancellationToken) =>
                Task.FromResult(new WifiSettingsResponse(policy, "1", []));

            public Task<Result<WifiSettingsResponse>> UpdatePolicyAsync(
                WifiPolicy requestedPolicy,
                string concurrencyToken,
                Guid actorUserId,
                Guid? branchId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException();

            public Task<Result<WifiNetworkResponse?>> SetNetworkAsync(
                Guid? branchIdScope,
                Guid branchId,
                string? ssid,
                WifiSecurity? security,
                string? password,
                Guid actorUserId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException();
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
