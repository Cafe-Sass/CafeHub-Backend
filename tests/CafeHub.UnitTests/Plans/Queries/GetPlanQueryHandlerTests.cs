using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Plans;
using CafeHub.Application.Plans.DTOs;
using CafeHub.Application.Plans.Queries;
using CafeHub.Domain.Plans;
using CafeHub.UnitTests.Plans.Domain;

namespace CafeHub.UnitTests.Plans.Queries
{
    public sealed class GetPlanQueryHandlerTests
    {
        private readonly FakePlanRepository repository = new();
        private readonly GetPlanQueryHandler handler;

        public GetPlanQueryHandlerTests()
        {
            handler = new GetPlanQueryHandler(repository);
        }

        [Fact]
        public async Task Handle_ForAKnownPlan_ReturnsVersionsNewestFirstAndEntitlementsByKey()
        {
            var plan = PlanTestData.CreatePlan(code: "growth", displayName: "Growth", isSelectable: false);
            var firstVersionId = Guid.NewGuid();
            PlanTestData.AddVersion(
                plan,
                versionId: firstVersionId,
                entitlements: new[]
                {
                    PlanTestData.CreateEntitlement(firstVersionId, key: "z_key", value: "1"),
                    PlanTestData.CreateEntitlement(firstVersionId, key: "a_key", value: "2"),
                });
            PlanTestData.AddVersion(plan, price: 49.99m, currencyCode: "eur", billingPeriod: BillingPeriod.Annual);
            repository.Add(plan);

            var result = await handler.Handle(new GetPlanQuery(plan.Id), CancellationToken.None);

            Assert.True(result.IsSuccess);
            var response = result.Value;
            Assert.Equal(plan.Id, response.Id);
            Assert.Equal("growth", response.Key);
            Assert.Equal("Growth", response.Name);
            Assert.False(response.IsSelectable);
            Assert.Equal(PlanTestData.CreatedAt, response.CreatedAt);
            Assert.Equal(PlanTestData.CreatedAt, response.UpdatedAt);
            Assert.Equal(new[] { 2, 1 }, response.Versions.Select(version => version.Number));
            Assert.Equal(49.99m, response.Versions[0].Price);
            Assert.Equal("eur", response.Versions[0].Currency);
            Assert.Equal("Annual", response.Versions[0].Period);
            Assert.Equal(new[] { "a_key", "z_key" }, response.Versions[1].Entitlements.Select(entitlement => entitlement.Key));
            Assert.Equal(1, repository.FindByIdCallCount);
        }

        [Fact]
        public async Task Handle_ForAnUnknownPlan_ReturnsNotFound()
        {
            var result = await handler.Handle(new GetPlanQuery(Guid.NewGuid()), CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(PlansErrors.NotFoundCode, result.Error!.Code);
            Assert.Equal(ErrorType.NotFound, result.Error.Type);
            Assert.Equal(PlansErrors.NotFoundMessage, result.Error.Message);
            Assert.Equal(1, repository.FindByIdCallCount);
        }

        [Fact]
        public async Task Handle_WithANullQuery_Throws()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
        }

        [Fact]
        public void TheHandler_RequiresTheRepository()
        {
            Assert.Throws<ArgumentNullException>(() => new GetPlanQueryHandler(null!));
        }

        [Fact]
        public void TheQuery_IsAQueryOfThePlanResponse()
        {
            Assert.True(typeof(IQuery<PlanResponse>).IsAssignableFrom(typeof(GetPlanQuery)));
            Assert.True(
                typeof(IQueryHandler<GetPlanQuery, PlanResponse>)
                    .IsAssignableFrom(typeof(GetPlanQueryHandler)));
        }
    }
}
