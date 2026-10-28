using CafeHub.Application.Plans.DTOs;
using CafeHub.Domain.Plans;
using CafeHub.UnitTests.Plans.Domain;

namespace CafeHub.UnitTests.Plans.Contracts
{
    public sealed class PlansResponseContractTests
    {
        [Fact]
        public void PlanResponse_ExposesExactlyTheDocumentedContract()
        {
            var properties = typeof(PlanResponse)
                .GetProperties()
                .OrderBy(property => property.Name)
                .ToArray();

            Assert.Equal(
                new[] { "CreatedAt", "Id", "IsSelectable", "Key", "Name", "UpdatedAt", "Versions" },
                properties.Select(property => property.Name));
            Assert.Equal(typeof(Guid), Property<PlanResponse>("Id").PropertyType);
            Assert.Equal(typeof(string), Property<PlanResponse>("Key").PropertyType);
            Assert.Equal(typeof(string), Property<PlanResponse>("Name").PropertyType);
            Assert.Equal(typeof(bool), Property<PlanResponse>("IsSelectable").PropertyType);
            Assert.Equal(typeof(IReadOnlyList<PlanVersionResponse>), Property<PlanResponse>("Versions").PropertyType);
            Assert.Equal(typeof(DateTimeOffset), Property<PlanResponse>("CreatedAt").PropertyType);
            Assert.Equal(typeof(DateTimeOffset), Property<PlanResponse>("UpdatedAt").PropertyType);
        }

        [Fact]
        public void PlanVersionResponse_ExposesOnlyTheDocumentedContract()
        {
            var properties = typeof(PlanVersionResponse)
                .GetProperties()
                .OrderBy(property => property.Name)
                .ToArray();

            Assert.Equal(
                new[] { "CreatedAt", "Currency", "Entitlements", "Number", "Period", "PlanVersionId", "Price", "UpdatedAt" },
                properties.Select(property => property.Name));
            Assert.Equal(typeof(Guid), Property<PlanVersionResponse>("PlanVersionId").PropertyType);
            Assert.Equal(typeof(int), Property<PlanVersionResponse>("Number").PropertyType);
            Assert.Equal(typeof(decimal), Property<PlanVersionResponse>("Price").PropertyType);
            Assert.Equal(typeof(string), Property<PlanVersionResponse>("Currency").PropertyType);
            Assert.Equal(typeof(string), Property<PlanVersionResponse>("Period").PropertyType);
            Assert.Equal(
                typeof(IReadOnlyList<PlanEntitlementResponse>),
                Property<PlanVersionResponse>("Entitlements").PropertyType);
        }

        [Fact]
        public void PlanEntitlementResponse_ExposesOnlyTheDocumentedContract()
        {
            var properties = typeof(PlanEntitlementResponse)
                .GetProperties()
                .OrderBy(property => property.Name)
                .ToArray();

            Assert.Equal(new[] { "Key", "Value" }, properties.Select(property => property.Name));
            Assert.Equal(typeof(string), Property<PlanEntitlementResponse>("Key").PropertyType);
            Assert.Equal(typeof(string), Property<PlanEntitlementResponse>("Value").PropertyType);
        }

        [Fact]
        public void PlanResponse_From_CopiesThePlanAndOrdersVersionsAndEntitlements()
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

            var response = PlanResponse.From(plan);

            Assert.Equal(plan.Id, response.Id);
            Assert.Equal("growth", response.Key);
            Assert.Equal("Growth", response.Name);
            Assert.False(response.IsSelectable);
            Assert.Equal(PlanTestData.CreatedAt, response.CreatedAt);
            Assert.Equal(PlanTestData.CreatedAt, response.UpdatedAt);
            Assert.Equal(new[] { 2, 1 }, response.Versions.Select(version => version.Number));
            Assert.Equal(firstVersionId, response.Versions[1].PlanVersionId);
            Assert.Equal("Annual", response.Versions[0].Period);
            Assert.Equal(new[] { "a_key", "z_key" }, response.Versions[1].Entitlements.Select(entitlement => entitlement.Key));
        }

        [Fact]
        public void TheMappingFactories_RejectNull()
        {
            Assert.Throws<ArgumentNullException>(() => PlanResponse.From(null!));
            Assert.Throws<ArgumentNullException>(() => PlanVersionResponse.From(null!));
        }

        private static System.Reflection.PropertyInfo Property<T>(string name)
        {
            return typeof(T).GetProperty(name) ?? throw new InvalidOperationException($"The property {name} was not found.");
        }
    }
}
