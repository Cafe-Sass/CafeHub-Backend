using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Dashboard;
using CafeHub.Application.Dashboard.DTOs;
using CafeHub.Application.Dashboard.Queries;
using MediatR;

namespace CafeHub.UnitTests.Dashboard
{
    public sealed class DashboardOverviewTests
    {
        private static readonly Guid BranchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid OtherBranchId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        private static readonly Guid ProductId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        private static readonly Guid ItemId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        private static readonly Guid BatchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly DateOnly From = new(2026, 11, 1);
        private static readonly DateOnly To = new(2026, 11, 8);

        private static DashboardSalesInsightsMetrics InsightMetrics() => new(
            [new DashboardBestSeller(ProductId, "Latte", 5, 50m)],
            [new DashboardBestSeller(ProductId, "Latte", 5, 50m)],
            [new DashboardPeakHour(9, 2)],
            new DashboardWifiAttachRate(0.5m, null),
            [new DashboardNearExpiryItem(ItemId, "Milk", BranchId, BatchId, new DateOnly(2026, 11, 14))]);

        private static DashboardResponse Dashboard() => new(
            DashboardMetricCatalog.All,
            new DashboardOverviewMetrics(
                120m,
                90m,
                4,
                30m,
                new DashboardPaymentSplit(60m, 30m),
                InsightMetrics().BestSellersByQuantity,
                InsightMetrics().BestSellersBySales,
                InsightMetrics().PeakHours,
                InsightMetrics().WifiAttachRate,
                InsightMetrics().NearExpiryItems));

        private static StubSender OverviewSender() => new(request => request switch
        {
            GetDashboardRevenueQuery => Result.Success(new DashboardRevenueResponse(
                DashboardMetricCatalog.All,
                new DashboardRevenueMetrics(120m, 90m, 4, 30m, new DashboardPaymentSplit(60m, 30m)))),
            GetDashboardSalesInsightsQuery => Result.Success(new DashboardSalesInsightsResponse(
                DashboardMetricCatalog.All,
                InsightMetrics())),
            _ => throw new InvalidOperationException($"Unexpected request {request.GetType().Name}."),
        });

        [Fact]
        public async Task Overview_CombinesRevenueAndSalesInsightsWithTheDefinitions()
        {
            var result = await new GetDashboardQueryHandler(OverviewSender()).Handle(
                new GetDashboardQuery { From = From, To = To },
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            var metrics = result.Value.Metrics;
            Assert.Equal(120m, metrics.Sales);
            Assert.Equal(90m, metrics.Collected);
            Assert.Equal(4, metrics.Orders);
            Assert.Equal(30m, metrics.AverageOrderValue);
            Assert.Equal(new DashboardPaymentSplit(60m, 30m), metrics.PaymentSplit);
            Assert.Equal(ProductId, Assert.Single(metrics.BestSellersByQuantity).ProductId);
            Assert.Equal(ProductId, Assert.Single(metrics.BestSellersBySales).ProductId);
            Assert.Equal(new DashboardPeakHour(9, 2), Assert.Single(metrics.PeakHours));
            Assert.Equal(0.5m, metrics.WifiAttachRate.Rate);
            Assert.Equal("Milk", Assert.Single(metrics.NearExpiryItems).ItemName);
            Assert.Equal(DashboardMetricCatalog.All, result.Value.MetricDefinitions);
        }

        [Fact]
        public async Task Overview_SendsTheSameRangeAndBranchToBothQueries()
        {
            var sender = OverviewSender();

            await new GetDashboardQueryHandler(sender).Handle(
                new GetDashboardQuery { From = From, To = To, BranchId = OtherBranchId },
                CancellationToken.None);

            Assert.Equal(2, sender.Requests.Count);
            var revenue = Assert.IsType<GetDashboardRevenueQuery>(sender.Requests[0]);
            var insights = Assert.IsType<GetDashboardSalesInsightsQuery>(sender.Requests[1]);
            Assert.Equal(From, revenue.From);
            Assert.Equal(To, revenue.To);
            Assert.Equal(OtherBranchId, revenue.BranchId);
            Assert.Equal(From, insights.From);
            Assert.Equal(To, insights.To);
            Assert.Equal(OtherBranchId, insights.BranchId);
        }

        [Fact]
        public async Task Overview_ReturnsTheRevenueFailureWithoutReadingTheInsights()
        {
            var sender = new StubSender(request => request is GetDashboardRevenueQuery
                ? Result.Failure<DashboardRevenueResponse>(
                    DashboardErrors.InvalidRange("The range end must be after the range start."))
                : throw new InvalidOperationException("The insights query must not run after a failed revenue query."));

            var result = await new GetDashboardQueryHandler(sender).Handle(
                new GetDashboardQuery { From = To, To = From },
                CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(DashboardErrors.InvalidRangeCode, result.Error!.Code);
            Assert.Single(sender.Requests);
        }

        [Fact]
        public async Task Overview_ReturnsTheInsightsFailure()
        {
            var sender = new StubSender(request => request switch
            {
                GetDashboardRevenueQuery => Result.Success(new DashboardRevenueResponse(
                    DashboardMetricCatalog.All,
                    new DashboardRevenueMetrics(0m, 0m, 0, null, new DashboardPaymentSplit(0m, 0m)))),
                GetDashboardSalesInsightsQuery => Result.Failure<DashboardSalesInsightsResponse>(
                    DashboardErrors.TimeZoneUnavailable()),
                _ => throw new InvalidOperationException("Unexpected request."),
            });

            var result = await new GetDashboardQueryHandler(sender).Handle(
                new GetDashboardQuery { From = From, To = To },
                CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(DashboardErrors.TimeZoneUnavailableCode, result.Error!.Code);
        }

        [Fact]
        public async Task Metric_ReturnsOneValueWithItsDefinitionForEveryKey()
        {
            var handler = MetricHandler(Dashboard());

            var sales = await handler.Handle(MetricQuery(DashboardMetricKeys.Sales), CancellationToken.None);
            var collected = await handler.Handle(MetricQuery(DashboardMetricKeys.Collected), CancellationToken.None);
            var orders = await handler.Handle(MetricQuery(DashboardMetricKeys.Orders), CancellationToken.None);
            var average = await handler.Handle(MetricQuery(DashboardMetricKeys.AverageOrderValue), CancellationToken.None);
            var split = await handler.Handle(MetricQuery(DashboardMetricKeys.PaymentSplit), CancellationToken.None);
            var bestSellers = await handler.Handle(MetricQuery(DashboardMetricKeys.BestSellers), CancellationToken.None);
            var peakHours = await handler.Handle(MetricQuery(DashboardMetricKeys.PeakHours), CancellationToken.None);
            var attachRate = await handler.Handle(MetricQuery(DashboardMetricKeys.WifiAttachRate), CancellationToken.None);
            var nearExpiry = await handler.Handle(MetricQuery(DashboardMetricKeys.NearExpiryItems), CancellationToken.None);

            Assert.Equal(DashboardMetricKeys.Sales, sales.Value.Definition.Key);
            Assert.Equal(120m, Assert.IsType<decimal>(sales.Value.Value));
            Assert.Equal(90m, Assert.IsType<decimal>(collected.Value.Value));
            Assert.Equal(4, Assert.IsType<int>(orders.Value.Value));
            Assert.Equal(30m, Assert.IsType<decimal>(average.Value.Value));
            Assert.Equal(new DashboardPaymentSplit(60m, 30m), Assert.IsType<DashboardPaymentSplit>(split.Value.Value));

            var ranked = Assert.IsType<DashboardBestSellers>(bestSellers.Value.Value);
            Assert.Equal(ProductId, Assert.Single(ranked.ByQuantity).ProductId);
            Assert.Equal(ProductId, Assert.Single(ranked.BySales).ProductId);

            var hours = Assert.IsAssignableFrom<IReadOnlyList<DashboardPeakHour>>(peakHours.Value.Value);
            Assert.Equal(new DashboardPeakHour(9, 2), Assert.Single(hours));
            Assert.Equal(0.5m, Assert.IsType<DashboardWifiAttachRate>(attachRate.Value.Value).Rate);
            var expiring = Assert.IsAssignableFrom<IReadOnlyList<DashboardNearExpiryItem>>(nearExpiry.Value.Value);
            Assert.Equal("Milk", Assert.Single(expiring).ItemName);

            foreach (var result in new[] { sales, collected, orders, average, split, bestSellers, peakHours, attachRate, nearExpiry })
            {
                Assert.True(result.IsSuccess);
                Assert.Contains(result.Value.Definition, DashboardMetricCatalog.All);
                Assert.False(string.IsNullOrWhiteSpace(result.Value.Definition.Formula));
            }
        }

        [Fact]
        public async Task Metric_AcceptsAnyLetterCaseOfACatalogKey()
        {
            var result = await MetricHandler(Dashboard()).Handle(
                MetricQuery("BESTSELLERS"),
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(DashboardMetricKeys.BestSellers, result.Value.Definition.Key);
        }

        [Fact]
        public async Task Metric_ReturnsNullAverageOrderValueWhenThereAreNoOrdersToDivideBy()
        {
            var dashboard = new DashboardResponse(
                DashboardMetricCatalog.All,
                new DashboardOverviewMetrics(
                    0m,
                    0m,
                    0,
                    null,
                    new DashboardPaymentSplit(0m, 0m),
                    [],
                    [],
                    [],
                    new DashboardWifiAttachRate(null, DashboardInsightsRules.NoDineInOrdersReason),
                    []));

            var result = await MetricHandler(dashboard).Handle(
                MetricQuery(DashboardMetricKeys.AverageOrderValue),
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Null(result.Value.Value);
        }

        [Fact]
        public async Task Metric_UnknownKeyReturnsNotFoundWithoutReadingTheDashboard()
        {
            var sender = new StubSender(_ => throw new InvalidOperationException("An unknown metric must not read data."));

            var result = await new GetDashboardMetricQueryHandler(sender).Handle(
                MetricQuery("profit"),
                CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(DashboardErrors.UnknownMetricCode, result.Error!.Code);
            Assert.Equal(ErrorType.NotFound, result.Error.Type);
            Assert.Empty(sender.Requests);
        }

        [Fact]
        public async Task Metric_PropagatesAFailedDashboard()
        {
            var sender = new StubSender(_ => Result.Failure<DashboardResponse>(
                DashboardErrors.InvalidRange("The range end must be after the range start.")));

            var result = await new GetDashboardMetricQueryHandler(sender).Handle(
                MetricQuery(DashboardMetricKeys.Sales),
                CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(DashboardErrors.InvalidRangeCode, result.Error!.Code);
        }

        private static GetDashboardMetricQueryHandler MetricHandler(DashboardResponse dashboard) =>
            new(new StubSender(_ => Result.Success(dashboard)));

        private static GetDashboardMetricQuery MetricQuery(string metric) => new()
        {
            Metric = metric,
            From = From,
            To = To,
        };

        private sealed class StubSender(Func<object, object?> respond) : ISender
        {
            public List<object> Requests { get; } = [];

            public Task<TResponse> Send<TResponse>(
                IRequest<TResponse> request, CancellationToken cancellationToken = default)
            {
                Requests.Add(request);
                return Task.FromResult((TResponse)respond(request)!);
            }

            public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
                where TRequest : IRequest =>
                throw new NotSupportedException("The dashboard sends only requests with a response.");

            public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException("The dashboard sends only typed requests.");

            public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
                IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException("The dashboard does not create streams.");

            public IAsyncEnumerable<object?> CreateStream(
                object request, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException("The dashboard does not create streams.");
        }
    }
}
