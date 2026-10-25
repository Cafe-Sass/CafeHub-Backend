using CafeHub.Domain.Plans;

namespace CafeHub.UnitTests.Plans.Domain
{
    public sealed class PlanTests
    {
        [Fact]
        public void Create_SetsThePlanState_AndStartsWithoutVersions()
        {
            var planId = Guid.NewGuid();

            var plan = Plan.Create(planId, "starter", "Starter", true, PlanTestData.CreatedAt, PlanTestData.AdminId);

            Assert.Equal(planId, plan.Id);
            Assert.Equal("starter", plan.Code);
            Assert.Equal("Starter", plan.DisplayName);
            Assert.True(plan.IsSelectable);
            Assert.Equal(PlanTestData.CreatedAt, plan.CreatedAt);
            Assert.Equal(PlanTestData.CreatedAt, plan.UpdatedAt);
            Assert.Equal(PlanTestData.AdminId, plan.CreatedBy);
            Assert.Equal(PlanTestData.AdminId, plan.UpdatedBy);
            Assert.Empty(plan.Versions);
        }

        [Fact]
        public void AddVersion_StartsAtOne_AndIncrementsForTheSamePlan()
        {
            var plan = PlanTestData.CreatePlan();

            var first = PlanTestData.AddVersion(plan);
            var second = PlanTestData.AddVersion(plan);
            var third = PlanTestData.AddVersion(plan);

            Assert.Equal(1, first.VersionNumber);
            Assert.Equal(2, second.VersionNumber);
            Assert.Equal(3, third.VersionNumber);
            Assert.Equal(new[] { first, second, third }, plan.Versions);
        }

        [Fact]
        public void AddVersion_KeepsVersionSequencesIndependentPerPlan()
        {
            var firstPlan = PlanTestData.CreatePlan(code: "starter");
            var secondPlan = PlanTestData.CreatePlan(code: "growth");

            PlanTestData.AddVersion(firstPlan);
            PlanTestData.AddVersion(firstPlan);
            var secondPlanFirstVersion = PlanTestData.AddVersion(secondPlan);
            var secondPlanSecondVersion = PlanTestData.AddVersion(secondPlan);

            Assert.Equal(new[] { 1, 2 }, firstPlan.Versions.Select(version => version.VersionNumber));
            Assert.Equal(new[] { 1, 2 }, secondPlan.Versions.Select(version => version.VersionNumber));
            Assert.Equal(1, secondPlanFirstVersion.VersionNumber);
            Assert.Equal(2, secondPlanSecondVersion.VersionNumber);
        }

        [Fact]
        public void AddVersion_StoresOnlyTheSuppliedEntitlements()
        {
            var plan = PlanTestData.CreatePlan();
            var versionId = Guid.NewGuid();
            var entitlement = PlanTestData.CreateEntitlement(versionId);

            var version = PlanTestData.AddVersion(plan, versionId, entitlements: new[] { entitlement });

            Assert.Same(entitlement, Assert.Single(version.Entitlements));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("ab")]
        [InlineData("Abc")]
        [InlineData("1abc")]
        [InlineData("abc_")]
        [InlineData("abc def")]
        public void Create_RejectsAnInvalidPlanCode(string? code)
        {
            Assert.Throws<ArgumentException>(() =>
                Plan.Create(Guid.NewGuid(), code!, "Starter", true, PlanTestData.CreatedAt));
        }

        [Fact]
        public void Create_AcceptsThePlanCodeLengthBoundaries()
        {
            var shortest = PlanTestData.CreatePlan(code: "abc");
            var longest = PlanTestData.CreatePlan(code: new string('a', 64));
            var withHyphen = PlanTestData.CreatePlan(code: "abc-def");

            Assert.Equal("abc", shortest.Code);
            Assert.Equal(64, longest.Code.Length);
            Assert.Equal("abc-def", withHyphen.Code);
        }

        [Fact]
        public void Create_RejectsAPlanCodeLongerThanSixtyFourCharacters()
        {
            Assert.Throws<ArgumentException>(() => PlanTestData.CreatePlan(code: new string('a', 65)));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Create_RejectsAMissingDisplayName(string? displayName)
        {
            Assert.Throws<ArgumentException>(() => PlanTestData.CreatePlan(displayName: displayName!));
        }

        [Fact]
        public void Create_AcceptsADisplayNameOfTwoHundredFiftySixCharacters()
        {
            var plan = PlanTestData.CreatePlan(displayName: new string('n', 256));

            Assert.Equal(256, plan.DisplayName.Length);
        }

        [Fact]
        public void Create_RejectsADisplayNameLongerThanTwoHundredFiftySixCharacters()
        {
            Assert.Throws<ArgumentException>(() => PlanTestData.CreatePlan(displayName: new string('n', 257)));
        }

        [Fact]
        public void Create_RejectsAnEmptyPlanId()
        {
            Assert.Throws<ArgumentException>(() =>
                Plan.Create(Guid.Empty, "starter", "Starter", true, PlanTestData.CreatedAt));
        }

        [Fact]
        public void AddVersion_RejectsAnEmptyVersionId_AndLeavesThePlanUnchanged()
        {
            var plan = PlanTestData.CreatePlan();

            Assert.Throws<ArgumentException>(() => PlanTestData.AddVersion(plan, Guid.Empty));

            Assert.Empty(plan.Versions);
            Assert.Equal(PlanTestData.CreatedAt, plan.UpdatedAt);
        }

        [Fact]
        public void AddVersion_RejectsDuplicateEntitlementKeys_AndLeavesThePlanUnchanged()
        {
            var plan = PlanTestData.CreatePlan();
            var versionId = Guid.NewGuid();
            var entitlements = new[]
            {
                PlanTestData.CreateEntitlement(versionId, key: "max_tables"),
                PlanTestData.CreateEntitlement(versionId, key: "max_tables"),
            };

            Assert.Throws<ArgumentException>(() =>
                PlanTestData.AddVersion(plan, versionId, entitlements: entitlements));

            Assert.Empty(plan.Versions);
            Assert.Equal(PlanTestData.CreatedAt, plan.UpdatedAt);
        }

        [Fact]
        public void AddVersion_RejectsAnEntitlementThatBelongsToAnotherVersion()
        {
            var plan = PlanTestData.CreatePlan();
            var versionId = Guid.NewGuid();
            var entitlement = PlanTestData.CreateEntitlement(Guid.NewGuid());

            Assert.Throws<ArgumentException>(() =>
                PlanTestData.AddVersion(plan, versionId, entitlements: new[] { entitlement }));

            Assert.Empty(plan.Versions);
        }

        [Fact]
        public void AddVersion_RejectsMissingEntitlements()
        {
            var plan = PlanTestData.CreatePlan();

            Assert.Throws<ArgumentNullException>(() =>
                plan.AddVersion(
                    Guid.NewGuid(),
                    19.99m,
                    "usd",
                    BillingPeriod.Monthly,
                    PlanVersionStatus.Active,
                    null!,
                    PlanTestData.CreatedAt,
                    PlanTestData.AdminId));

            Assert.Empty(plan.Versions);
        }
    }
}
