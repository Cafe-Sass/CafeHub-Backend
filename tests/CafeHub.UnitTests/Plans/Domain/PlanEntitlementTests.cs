using System.Reflection;
using CafeHub.Domain.Plans;

namespace CafeHub.UnitTests.Plans.Domain
{
    public sealed class PlanEntitlementTests
    {
        [Fact]
        public void Create_StoresTheKeyValueAndVersion()
        {
            var entitlementId = Guid.NewGuid();
            var versionId = Guid.NewGuid();

            var entitlement = PlanEntitlement.Create(
                entitlementId,
                versionId,
                "max_tables.v2",
                "10",
                PlanTestData.CreatedAt);

            Assert.Equal(entitlementId, entitlement.Id);
            Assert.Equal(versionId, entitlement.PlanVersionId);
            Assert.Equal("max_tables.v2", entitlement.Key);
            Assert.Equal("10", entitlement.Value);
            Assert.Equal(PlanTestData.CreatedAt, entitlement.CreatedAt);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Max_Tables")]
        [InlineData("1tables")]
        [InlineData("max-tables")]
        [InlineData("max tables")]
        [InlineData("max/tables")]
        public void Create_RejectsAnInvalidEntitlementKey(string? key)
        {
            Assert.Throws<ArgumentException>(() =>
                PlanEntitlement.Create(Guid.NewGuid(), Guid.NewGuid(), key!, "10", PlanTestData.CreatedAt));
        }

        [Fact]
        public void Create_AcceptsTheEntitlementKeyLengthBoundaries()
        {
            var shortest = PlanEntitlement.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "a",
                "true",
                PlanTestData.CreatedAt);
            var longest = PlanEntitlement.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                new string('a', 128),
                "true",
                PlanTestData.CreatedAt);

            Assert.Equal("a", shortest.Key);
            Assert.Equal(128, longest.Key.Length);
        }

        [Fact]
        public void Create_RejectsAnEntitlementKeyLongerThanOneHundredTwentyEightCharacters()
        {
            Assert.Throws<ArgumentException>(() =>
                PlanEntitlement.Create(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    new string('a', 129),
                    "true",
                    PlanTestData.CreatedAt));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Create_RejectsAMissingEntitlementValue(string? value)
        {
            Assert.Throws<ArgumentException>(() =>
                PlanEntitlement.Create(Guid.NewGuid(), Guid.NewGuid(), "max_tables", value!, PlanTestData.CreatedAt));
        }

        [Fact]
        public void Create_AcceptsAValueOfFiveHundredTwelveCharacters()
        {
            var entitlement = PlanEntitlement.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "max_tables",
                new string('v', 512),
                PlanTestData.CreatedAt);

            Assert.Equal(512, entitlement.Value.Length);
        }

        [Fact]
        public void Create_RejectsAValueLongerThanFiveHundredTwelveCharacters()
        {
            Assert.Throws<ArgumentException>(() =>
                PlanEntitlement.Create(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "max_tables",
                    new string('v', 513),
                    PlanTestData.CreatedAt));
        }

        [Fact]
        public void Create_RejectsEmptyIdentifiers()
        {
            Assert.Throws<ArgumentException>(() =>
                PlanEntitlement.Create(Guid.Empty, Guid.NewGuid(), "max_tables", "10", PlanTestData.CreatedAt));
            Assert.Throws<ArgumentException>(() =>
                PlanEntitlement.Create(Guid.NewGuid(), Guid.Empty, "max_tables", "10", PlanTestData.CreatedAt));
        }

        [Fact]
        public void AnEntitlement_ExposesNoPublicMutators()
        {
            var publicSetters = typeof(PlanEntitlement)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.SetMethod is { IsPublic: true })
                .ToArray();

            var publicDeclaredMethods = typeof(PlanEntitlement)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName)
                .ToArray();

            Assert.Empty(publicSetters);
            Assert.Empty(publicDeclaredMethods);
        }
    }
}
