using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.CafeProfile.Contracts;
using CafeHub.Application.CafeProfile.DTOs;
using CafeHub.Application.Dashboard;
using CafeHub.Application.Dashboard.Contracts;
using CafeHub.Application.Dashboard.DTOs;
using CafeHub.Application.Identity;
using CafeHub.Application.Insights;
using CafeHub.Application.Insights.Queries;
using CafeHub.Infrastructure.Insights;
using FluentValidation.TestHelper;
using Microsoft.Extensions.Logging.Abstractions;

namespace CafeHub.UnitTests.Insights
{
    public sealed class GetInsightsQueryTests
    {
        private static readonly Guid BranchA = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid BranchB = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid ActorId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly DateTimeOffset OccurredAt = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
        private static readonly DateTimeOffset Now = new(2026, 11, 28, 9, 30, 0, TimeSpan.Zero);

        [Fact]
        public async Task Handle_ProviderAbsent_UsesTheRulesFromLeakageAndNearExpiry()
        {
            var leakage = new FakeLeakageStore(new DashboardLeakageSnapshot(
                new[] { new DashboardLeakageRecord(Guid.NewGuid(), 25.50m, OccurredAt) },
                new[] { new DashboardLeakageBillRecord(Guid.NewGuid(), 100m, 60m, OccurredAt) },
                new[] { new DashboardLeakageRecord(Guid.NewGuid(), 30m, OccurredAt) }));
            var insights = new FakeInsightsStore(new DashboardInsightsSnapshot(
                [],
                [],
                0,
                0,
                new[] { new DashboardNearExpiryRow(Guid.NewGuid(), "Milk", BranchA, Guid.NewGuid(), new DateOnly(2026, 11, 14)) }));
            var handler = Handler(leakage, insights, new NotConfiguredBusinessInsightProvider());

            var result = await handler.Handle(Query(), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(BusinessInsightSources.Rules, result.Value.Source);
            Assert.Equal(5, result.Value.Cards.Count);
            Assert.Equal(Now, result.Value.GeneratedAt);
            Assert.Null(result.Value.Reason);
            Assert.Contains(
                result.Value.Cards,
                card => card.Evidence.Any(evidence =>
                    evidence.Key == DashboardLeakageKeys.SalesCollectedGap && evidence.Value == 40m));
            Assert.All(result.Value.Cards, card =>
            {
                Assert.False(string.IsNullOrWhiteSpace(card.Title));
                Assert.False(string.IsNullOrWhiteSpace(card.Recommendation));
                Assert.InRange(card.Confidence, 0, 1);
                Assert.All(card.Evidence, evidence =>
                    Assert.True(
                        DashboardLeakageKeys.All.Contains(evidence.Key)
                        || evidence.Key == DashboardMetricKeys.NearExpiryItems));
            });
        }

        [Fact]
        public async Task Handle_EmptyData_ReturnsAnEmptyListWithAReason()
        {
            var handler = Handler(
                new FakeLeakageStore(new DashboardLeakageSnapshot([], [], [])),
                new FakeInsightsStore(new DashboardInsightsSnapshot([], [], 0, 0, [])),
                new NotConfiguredBusinessInsightProvider());

            var result = await handler.Handle(Query(), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Empty(result.Value.Cards);
            Assert.Equal(InsightsReasons.NoSignals, result.Value.Reason);
            Assert.Equal(BusinessInsightSources.Rules, result.Value.Source);
        }

        [Fact]
        public async Task Handle_ThrowingProvider_StillReturnsTheRules()
        {
            var handler = Handler(
                PopulatedLeakageStore(),
                new FakeInsightsStore(new DashboardInsightsSnapshot([], [], 0, 0, [])),
                new ThrowingProvider());

            var result = await handler.Handle(Query(), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(BusinessInsightSources.Rules, result.Value.Source);
            Assert.NotEmpty(result.Value.Cards);
        }

        [Fact]
        public async Task Handle_ValidProviderCards_MapAndCapAtTen()
        {
            var cards = Enumerable.Range(0, 12)
                .Select(_ => new BusinessInsightCard(
                    "Provider title",
                    new[] { new BusinessInsightEvidence(DashboardLeakageKeys.RejectedAndExpiredOrders, 12.5m) },
                    "Provider recommendation",
                    0.85))
                .ToList();
            var handler = Handler(
                PopulatedLeakageStore(),
                new FakeInsightsStore(new DashboardInsightsSnapshot([], [], 0, 0, [])),
                new FakeProvider(Result.Success<IReadOnlyList<BusinessInsightCard>>(cards)));

            var result = await handler.Handle(Query(), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(BusinessInsightSources.Ai, result.Value.Source);
            Assert.Equal(BusinessInsightRules.MaximumCards, result.Value.Cards.Count);
            var card = result.Value.Cards[0];
            Assert.Equal("Provider title", card.Title);
            Assert.Equal("Provider recommendation", card.Recommendation);
            Assert.Equal(0.85, card.Confidence);
            var evidence = Assert.Single(card.Evidence);
            Assert.Equal(DashboardLeakageKeys.RejectedAndExpiredOrders, evidence.Key);
            Assert.Equal(12.5m, evidence.Value);
        }

        [Fact]
        public async Task Handle_StaffBranch_ReplacesTheRequestedBranchInBothReads()
        {
            var leakage = PopulatedLeakageStore();
            var insights = new FakeInsightsStore(new DashboardInsightsSnapshot([], [], 0, 0, []));
            var staff = new TenantAccountAccess(true, "1", true) { BranchId = BranchB };
            var handler = Handler(
                leakage,
                insights,
                new NotConfiguredBusinessInsightProvider(),
                staff);

            await handler.Handle(Query(), CancellationToken.None);

            Assert.Equal(BranchB, leakage.LastRequest!.BranchId);
            Assert.Equal(BranchB, insights.LastRequest!.BranchId);
        }

        [Fact]
        public async Task Handle_OwnerWithoutBranch_ReadsEveryBranch()
        {
            var leakage = PopulatedLeakageStore();
            var insights = new FakeInsightsStore(new DashboardInsightsSnapshot([], [], 0, 0, []));
            var handler = Handler(
                leakage,
                insights,
                new NotConfiguredBusinessInsightProvider(),
                new TenantAccountAccess(false, "1", false));

            await handler.Handle(Query(), CancellationToken.None);

            Assert.Null(leakage.LastRequest!.BranchId);
            Assert.Null(insights.LastRequest!.BranchId);
        }

        [Fact]
        public async Task Handle_MissingTimeZone_FailsWithoutReadingTheStores()
        {
            var leakage = PopulatedLeakageStore();
            var handler = new GetInsightsQueryHandler(
                leakage,
                new FakeInsightsStore(new DashboardInsightsSnapshot([], [], 0, 0, [])),
                Generator(new NotConfiguredBusinessInsightProvider()),
                new FakeSettingsReader(null),
                new FakeCurrentTenantUser(new TenantAccountAccess(false, "1", false)),
                new FixedTimeProvider(Now));

            var result = await handler.Handle(Query(), CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(DashboardErrors.TimeZoneUnavailableCode, result.Error!.Code);
            Assert.Null(leakage.LastRequest);
        }

        [Fact]
        public async Task Handle_InvalidRange_FailsWithTheDashboardRangeError()
        {
            var handler = Handler(
                PopulatedLeakageStore(),
                new FakeInsightsStore(new DashboardInsightsSnapshot([], [], 0, 0, [])),
                new NotConfiguredBusinessInsightProvider());

            var result = await handler.Handle(
                Query() with { From = new DateOnly(2026, 11, 2), To = new DateOnly(2026, 11, 1) },
                CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(DashboardErrors.InvalidRangeCode, result.Error!.Code);
        }

        [Fact]
        public void Validator_RejectsAnInvalidRangeAndAnEmptyBranch()
        {
            var validator = new GetInsightsQueryValidator();

            validator.TestValidate(new GetInsightsQuery())
                .ShouldHaveValidationErrorFor(query => query.From);
            validator.TestValidate(new GetInsightsQuery
            {
                From = new DateOnly(2026, 11, 2),
                To = new DateOnly(2026, 11, 1),
            }).ShouldHaveValidationErrorFor(query => query.To);
            validator.TestValidate(Query() with { BranchId = Guid.Empty })
                .ShouldHaveValidationErrorFor(query => query.BranchId);
            validator.TestValidate(Query() with
            {
                From = new DateOnly(2026, 1, 1),
                To = new DateOnly(2026, 1, 1).AddDays(DashboardDateRange.MaximumDays + 1),
            }).ShouldHaveValidationErrorFor(query => query.To);
            validator.TestValidate(Query()).ShouldNotHaveAnyValidationErrors();
        }

        private static GetInsightsQuery Query() => new()
        {
            From = new DateOnly(2026, 11, 1),
            To = new DateOnly(2026, 12, 1),
        };

        private static FakeLeakageStore PopulatedLeakageStore() =>
            new(new DashboardLeakageSnapshot(
                new[] { new DashboardLeakageRecord(Guid.NewGuid(), 25.50m, OccurredAt) },
                [],
                []));

        private static BusinessInsightGenerator Generator(IBusinessInsightProvider provider) =>
            new(provider, NullLogger<BusinessInsightGenerator>.Instance);

        private static GetInsightsQueryHandler Handler(
            FakeLeakageStore leakage,
            FakeInsightsStore insights,
            IBusinessInsightProvider provider,
            TenantAccountAccess? access = null) =>
            new(
                leakage,
                insights,
                Generator(provider),
                new FakeSettingsReader("UTC"),
                new FakeCurrentTenantUser(access ?? new TenantAccountAccess(false, "1", false)),
                new FixedTimeProvider(Now));

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

        private sealed class FakeProvider(Result<IReadOnlyList<BusinessInsightCard>> result)
            : IBusinessInsightProvider
        {
            public Task<Result<IReadOnlyList<BusinessInsightCard>>> GetAsync(
                BusinessInsightInput input,
                CancellationToken cancellationToken) =>
                Task.FromResult(result);
        }

        private sealed class ThrowingProvider : IBusinessInsightProvider
        {
            public Task<Result<IReadOnlyList<BusinessInsightCard>>> GetAsync(
                BusinessInsightInput input,
                CancellationToken cancellationToken) =>
                throw new InvalidOperationException("The provider is unavailable.");
        }

        private sealed class FakeSettingsReader(string? timezone) : ICafeSettingsReader
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
                    Timezone = timezone ?? string.Empty,
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

        private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => now;
        }
    }
}
