using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Dashboard;
using CafeHub.Application.Dashboard.DTOs;
using CafeHub.Application.Insights;
using CafeHub.Infrastructure.Insights;
using Microsoft.Extensions.Logging.Abstractions;

namespace CafeHub.UnitTests.Insights
{
    public sealed class BusinessInsightTests
    {
        private static readonly Guid BranchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid ContributorId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly DateTimeOffset OccurredAt = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

        [Fact]
        public void Validate_ValidCardsPassUnchanged()
        {
            var input = Input();
            var cards = new[]
            {
                Card(DashboardLeakageKeys.RejectedAndExpiredOrders, 0.7),
                Card(DashboardMetricKeys.NearExpiryItems, 1),
            };

            var valid = BusinessInsightValidator.Validate(input, cards);

            Assert.Equal(2, valid.Count);
            Assert.Equal(cards[0], valid[0]);
            Assert.Equal(cards[1], valid[1]);
        }

        [Theory]
        [InlineData("unknownKey")]
        [InlineData("emptyRecommendation")]
        [InlineData("emptyTitle")]
        [InlineData("emptyEvidence")]
        [InlineData("confidenceAboveOne")]
        [InlineData("negativeConfidence")]
        [InlineData("notANumber")]
        public void Validate_DropsCardsWithUnknownKeysEmptyTextOrOutOfRangeConfidence(string invalidCase)
        {
            var cards = new[] { InvalidCard(invalidCase) };

            var valid = BusinessInsightValidator.Validate(Input(), cards);

            Assert.Empty(valid);
        }

        [Fact]
        public void Validate_KeepsAValidCardNextToDroppedOnes()
        {
            var valid = Card(DashboardLeakageKeys.RejectedAndExpiredOrders, 0.5);
            var cards = new[]
            {
                InvalidCard("unknownKey"),
                valid,
                InvalidCard("confidenceAboveOne"),
            };

            var result = BusinessInsightValidator.Validate(Input(), cards);

            Assert.Equal(new[] { valid }, result);
        }

        [Fact]
        public void Validate_NullCardsYieldNoInsights()
        {
            Assert.Empty(BusinessInsightValidator.Validate(Input(), null));
        }

        [Fact]
        public void Generate_ProducesDeterministicCardsFromLeakageAndNearExpiry()
        {
            var input = Input();

            var cards = BusinessInsightRules.Generate(input);

            Assert.Equal(
                new[]
                {
                    BusinessInsightRules.RejectedAndExpiredOrdersTitle,
                    BusinessInsightRules.BillsClosedWithAmountOwedTitle,
                    BusinessInsightRules.SalesCollectedGapTitle,
                    BusinessInsightRules.NearExpiryItemsTitle,
                },
                cards.Select(card => card.Title).ToArray());
            Assert.Equal(
                new[]
                {
                    DashboardLeakageKeys.RejectedAndExpiredOrders,
                    DashboardLeakageKeys.BillsClosedWithAmountOwed,
                    DashboardLeakageKeys.SalesCollectedGap,
                    DashboardMetricKeys.NearExpiryItems,
                },
                cards.Select(card => Assert.Single(card.Evidence).Key).ToArray());
            Assert.Equal(35.50m, cards[0].Evidence[0].Value);
            Assert.All(cards, card =>
            {
                Assert.False(string.IsNullOrWhiteSpace(card.Recommendation));
                Assert.InRange(card.Confidence, 0, 1);
            });
            Assert.Equal(
                cards.Select(card => card.Title).ToArray(),
                BusinessInsightRules.Generate(input).Select(card => card.Title).ToArray());
        }

        [Fact]
        public void Generate_SkipsSignalsWithoutData()
        {
            var input = new BusinessInsightInput(new[]
            {
                new BusinessInsightMetric(DashboardLeakageKeys.RejectedAndExpiredOrders, 0m, 0),
                new BusinessInsightMetric(DashboardLeakageKeys.SalesCollectedGap, 0m, 0),
                new BusinessInsightMetric(DashboardMetricKeys.NearExpiryItems, 0, 0),
            });

            Assert.Empty(BusinessInsightRules.Generate(input));
            Assert.Empty(BusinessInsightRules.Generate(BusinessInsightInput.Empty));
        }

        [Fact]
        public async Task Generate_AThrowingProviderFallsBackToRulesWithoutThrowing()
        {
            var generator = Generator(new FakeProvider { Exception = new InvalidOperationException("boom") });

            var result = await generator.GenerateAsync(Input(), CancellationToken.None);

            Assert.Equal(BusinessInsightSources.Rules, result.Source);
            Assert.Equal(
                BusinessInsightRules.Generate(Input()).Select(card => card.Title).ToArray(),
                result.Cards.Select(card => card.Title).ToArray());
        }

        [Fact]
        public async Task Generate_InvalidProviderOutputFallsBackToRules()
        {
            var generator = Generator(new FakeProvider
            {
                ProviderResult = Result.Success<IReadOnlyList<BusinessInsightCard>>(
                    new[] { Card("guestName", 0.9) }),
            });

            var result = await generator.GenerateAsync(Input(), CancellationToken.None);

            Assert.Equal(BusinessInsightSources.Rules, result.Source);
            Assert.DoesNotContain(result.Cards, card => card.Evidence.Any(evidence => evidence.Key == "guestName"));
        }

        [Fact]
        public async Task Generate_AFailedProviderFallsBackToRules()
        {
            var generator = Generator(new FakeProvider
            {
                ProviderResult = Result.Failure<IReadOnlyList<BusinessInsightCard>>(
                    Error.Failure("Insights.ProviderFailure", "The provider is unavailable.")),
            });

            var result = await generator.GenerateAsync(Input(), CancellationToken.None);

            Assert.Equal(BusinessInsightSources.Rules, result.Source);
        }

        [Fact]
        public async Task Generate_ValidProviderOutputIsReturnedAsAi()
        {
            var card = Card(DashboardLeakageKeys.RejectedAndExpiredOrders, 0.9);
            var generator = Generator(new FakeProvider
            {
                ProviderResult = Result.Success<IReadOnlyList<BusinessInsightCard>>(new[] { card }),
            });

            var result = await generator.GenerateAsync(Input(), CancellationToken.None);

            Assert.Equal(BusinessInsightSources.Ai, result.Source);
            var returned = Assert.Single(result.Cards);
            Assert.Equal(card.Title, returned.Title);
            Assert.Equal(card.Recommendation, returned.Recommendation);
            Assert.Equal(card.Confidence, returned.Confidence);
            Assert.Equal(card.Evidence, returned.Evidence);
        }

        [Fact]
        public async Task Generate_NotConfiguredProviderAlwaysYieldsTheFallback()
        {
            var provider = new NotConfiguredBusinessInsightProvider();
            var generator = Generator(provider);

            var first = await generator.GenerateAsync(Input(), CancellationToken.None);
            var second = await generator.GenerateAsync(Input(), CancellationToken.None);

            Assert.Equal(BusinessInsightSources.Rules, first.Source);
            Assert.Equal(BusinessInsightSources.Rules, second.Source);
            Assert.NotEmpty(first.Cards);
            Assert.Empty((await provider.GetAsync(Input(), CancellationToken.None)).Value);
        }

        [Fact]
        public async Task Generate_CancellationPropagatesInsteadOfFallingBack()
        {
            using var source = new CancellationTokenSource();
            await source.CancelAsync();
            var generator = Generator(new FakeProvider { Exception = new OperationCanceledException() });

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => generator.GenerateAsync(Input(), source.Token));
        }

        [Fact]
        public void InputBuilder_EmitsOnlyAggregatedMetrics()
        {
            var signals = new[]
            {
                new DashboardLeakageSignal(
                    DashboardLeakageKeys.RejectedAndExpiredOrders,
                    2,
                    35.50m,
                    1,
                    false,
                    new[] { new DashboardLeakageContributor(ContributorId, 35.50m, OccurredAt) }),
                new DashboardLeakageSignal(
                    DashboardLeakageKeys.RejectedPaymentProofs,
                    0,
                    0m,
                    1,
                    false,
                    Array.Empty<DashboardLeakageContributor>()),
            };
            var nearExpiryItems = new[]
            {
                new DashboardNearExpiryItem(Guid.NewGuid(), "Milk", BranchId, Guid.NewGuid(), new DateOnly(2026, 11, 14)),
                new DashboardNearExpiryItem(Guid.NewGuid(), "Bread", BranchId, Guid.NewGuid(), new DateOnly(2026, 11, 15)),
            };

            var input = BusinessInsightInputBuilder.From(signals, nearExpiryItems);

            Assert.Equal(
                new[]
                {
                    DashboardLeakageKeys.RejectedAndExpiredOrders,
                    DashboardLeakageKeys.RejectedPaymentProofs,
                    DashboardMetricKeys.NearExpiryItems,
                },
                input.Metrics.Select(metric => metric.Key).ToArray());
            Assert.Equal(2, input.Metrics[0].Count);
            Assert.Equal(35.50m, input.Metrics[0].Value);
            Assert.Equal(2, input.Metrics[2].Value);
            Assert.Equal(2, input.Metrics[2].Count);
            Assert.All(input.Metrics, metric =>
                Assert.True(
                    DashboardLeakageKeys.All.Contains(metric.Key)
                    || metric.Key == DashboardMetricKeys.NearExpiryItems));
            Assert.DoesNotContain(
                input.Metrics,
                metric => metric.Key.Contains("Milk", StringComparison.Ordinal)
                    || metric.Key.Contains(ContributorId.ToString(), StringComparison.Ordinal));
        }

        private static BusinessInsightGenerator Generator(IBusinessInsightProvider provider) =>
            new(provider, NullLogger<BusinessInsightGenerator>.Instance);

        private static BusinessInsightInput Input() => new(new[]
        {
            new BusinessInsightMetric(DashboardLeakageKeys.RejectedAndExpiredOrders, 35.50m, 2),
            new BusinessInsightMetric(DashboardLeakageKeys.BillsClosedWithAmountOwed, 40m, 1),
            new BusinessInsightMetric(DashboardLeakageKeys.RejectedPaymentProofs, 0m, 0),
            new BusinessInsightMetric(DashboardLeakageKeys.SalesCollectedGap, 40m, 1),
            new BusinessInsightMetric(DashboardMetricKeys.NearExpiryItems, 1, 1),
        });

        private static BusinessInsightCard Card(string key, double confidence) =>
            new(
                "A title",
                new[] { new BusinessInsightEvidence(key, 1m) },
                "A recommendation",
                confidence);

        private static BusinessInsightCard EmptyEvidenceCard() =>
            new("A title", Array.Empty<BusinessInsightEvidence>(), "A recommendation", 0.5);

        private static BusinessInsightCard InvalidCard(string invalidCase) => invalidCase switch
        {
            "unknownKey" => Card(DashboardMetricKeys.WifiAttachRate, 0.5),
            "emptyRecommendation" => Card(DashboardLeakageKeys.RejectedAndExpiredOrders, 0.5) with { Recommendation = " " },
            "emptyTitle" => Card(DashboardLeakageKeys.RejectedAndExpiredOrders, 0.5) with { Title = "" },
            "emptyEvidence" => EmptyEvidenceCard(),
            "confidenceAboveOne" => Card(DashboardLeakageKeys.RejectedAndExpiredOrders, 1.1),
            "negativeConfidence" => Card(DashboardLeakageKeys.RejectedAndExpiredOrders, -0.1),
            "notANumber" => Card(DashboardLeakageKeys.RejectedAndExpiredOrders, double.NaN),
            _ => throw new ArgumentOutOfRangeException(nameof(invalidCase)),
        };

        private sealed class FakeProvider : IBusinessInsightProvider
        {
            public Result<IReadOnlyList<BusinessInsightCard>>? ProviderResult { get; init; }

            public Exception? Exception { get; init; }

            public Task<Result<IReadOnlyList<BusinessInsightCard>>> GetAsync(
                BusinessInsightInput input,
                CancellationToken cancellationToken)
            {
                if (Exception is not null)
                {
                    throw Exception;
                }

                return Task.FromResult(
                    ProviderResult
                    ?? Result.Success<IReadOnlyList<BusinessInsightCard>>(Array.Empty<BusinessInsightCard>()));
            }
        }
    }
}
