using System.Reflection;
using CafeHub.Domain.Plans;

namespace CafeHub.UnitTests.Plans.Domain
{
    public sealed class PlanVersionTests
    {
        [Fact]
        public void AddVersion_StoresTheTermsAndEntitlements()
        {
            var plan = PlanTestData.CreatePlan();
            var versionId = Guid.NewGuid();
            var entitlement = PlanTestData.CreateEntitlement(versionId, key: "max_tables.v2", value: "25");

            var version = plan.AddVersion(
                versionId,
                49.95m,
                "usd",
                BillingPeriod.Annual,
                PlanVersionStatus.Active,
                new[] { entitlement },
                PlanTestData.CreatedAt,
                PlanTestData.AdminId);

            Assert.Equal(versionId, version.Id);
            Assert.Equal(plan.Id, version.PlanId);
            Assert.Equal(1, version.VersionNumber);
            Assert.Equal(49.95m, version.Price);
            Assert.Equal("usd", version.CurrencyCode);
            Assert.Equal(BillingPeriod.Annual, version.BillingPeriod);
            Assert.Equal(PlanVersionStatus.Active, version.Status);
            Assert.Equal(PlanTestData.CreatedAt, version.CreatedAt);
            Assert.Equal(PlanTestData.CreatedAt, version.UpdatedAt);
            Assert.Equal(PlanTestData.AdminId, version.CreatedBy);
            Assert.Equal(PlanTestData.AdminId, version.UpdatedBy);
            Assert.Same(entitlement, Assert.Single(version.Entitlements));
        }

        [Fact]
        public void AddVersion_AcceptsAZeroPrice()
        {
            var plan = PlanTestData.CreatePlan();

            var version = PlanTestData.AddVersion(plan, price: 0m);

            Assert.Equal(0m, version.Price);
        }

        [Fact]
        public void AddVersion_RejectsNegativeOrOverScalePrices()
        {
            var plan = PlanTestData.CreatePlan();

            Assert.Throws<ArgumentOutOfRangeException>(() => PlanTestData.AddVersion(plan, price: -0.01m));
            Assert.Throws<ArgumentOutOfRangeException>(() => PlanTestData.AddVersion(plan, price: 19.999m));
            Assert.Throws<ArgumentOutOfRangeException>(() => PlanTestData.AddVersion(plan, price: 19.990m));
            Assert.Throws<ArgumentOutOfRangeException>(() => PlanTestData.AddVersion(plan, price: 10_000_000_000.00m));
            Assert.Empty(plan.Versions);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("USD")]
        [InlineData("us")]
        [InlineData("usdd")]
        [InlineData("u1d")]
        [InlineData("usd ")]
        [InlineData(" usd")]
        public void AddVersion_RejectsAnInvalidCurrencyCode(string? currencyCode)
        {
            var plan = PlanTestData.CreatePlan();

            Assert.Throws<ArgumentException>(() =>
                PlanTestData.AddVersion(plan, currencyCode: currencyCode!));

            Assert.Empty(plan.Versions);
        }

        [Fact]
        public void AddVersion_RejectsAnUndefinedBillingPeriod()
        {
            var plan = PlanTestData.CreatePlan();

            Assert.Throws<ArgumentException>(() =>
                PlanTestData.AddVersion(plan, billingPeriod: (BillingPeriod)99));

            Assert.Empty(plan.Versions);
        }

        [Fact]
        public void AddVersion_RejectsAnUndefinedStatus()
        {
            var plan = PlanTestData.CreatePlan();

            Assert.Throws<ArgumentException>(() =>
                PlanTestData.AddVersion(plan, status: (PlanVersionStatus)99));

            Assert.Empty(plan.Versions);
        }

        [Fact]
        public void APlanVersion_ExposesNoPublicMutators()
        {
            var publicSetters = typeof(PlanVersion)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.SetMethod is { IsPublic: true })
                .ToArray();

            var publicDeclaredMethods = typeof(PlanVersion)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName)
                .ToArray();

            Assert.Empty(publicSetters);
            Assert.Empty(publicDeclaredMethods);
        }

        [Fact]
        public void APlanVersion_ExposesItsEntitlementsAsAReadOnlyCollection()
        {
            var plan = PlanTestData.CreatePlan();
            var versionId = Guid.NewGuid();
            var version = PlanTestData.AddVersion(
                plan,
                versionId,
                entitlements: new[] { PlanTestData.CreateEntitlement(versionId) });

            var entitlements = Assert.IsAssignableFrom<IList<PlanEntitlement>>(version.Entitlements);

            Assert.Throws<NotSupportedException>(() =>
                entitlements.Add(PlanTestData.CreateEntitlement(versionId, key: "another_key")));
        }

        [Fact]
        public void TheSameEntitlementKey_IsAllowedInDifferentVersions()
        {
            var plan = PlanTestData.CreatePlan();
            var firstVersionId = Guid.NewGuid();
            var secondVersionId = Guid.NewGuid();

            var firstVersion = PlanTestData.AddVersion(
                plan,
                firstVersionId,
                entitlements: new[] { PlanTestData.CreateEntitlement(firstVersionId, key: "max_tables", value: "10") });
            var secondVersion = PlanTestData.AddVersion(
                plan,
                secondVersionId,
                entitlements: new[] { PlanTestData.CreateEntitlement(secondVersionId, key: "max_tables", value: "20") });

            Assert.Equal("10", Assert.Single(firstVersion.Entitlements).Value);
            Assert.Equal("20", Assert.Single(secondVersion.Entitlements).Value);
        }

        [Fact]
        public void TheEnumLabels_MatchTheDatabaseContract()
        {
            Assert.Equal(new[] { "Monthly", "Annual" }, Enum.GetNames<BillingPeriod>());
            Assert.Equal(new[] { "Draft", "Active", "Retired" }, Enum.GetNames<PlanVersionStatus>());
        }
    }
}
