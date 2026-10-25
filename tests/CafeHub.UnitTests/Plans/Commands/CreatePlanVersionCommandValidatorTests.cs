using CafeHub.Application.Plans.Commands;

namespace CafeHub.UnitTests.Plans.Commands
{
    public sealed class CreatePlanVersionCommandValidatorTests
    {
        private static readonly Guid PlanId = Guid.NewGuid();

        private readonly CreatePlanVersionCommandValidator validator = new();

        private static CreatePlanVersionCommand CreateCommand(
            Guid? planId = null,
            decimal price = 19.99m,
            string currency = "usd",
            string period = "Monthly",
            IReadOnlyList<PlanEntitlementRequest>? entitlements = null)
        {
            return new CreatePlanVersionCommand(
                planId ?? PlanId,
                price,
                currency,
                period,
                entitlements ?? new[] { new PlanEntitlementRequest("max_tables", "10") });
        }

        [Fact]
        public void Validate_WithAValidCommand_IsValid()
        {
            var result = validator.Validate(CreateCommand());

            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(19.99)]
        [InlineData(9999999999.99)]
        public void Validate_WithABoundaryPrice_IsValid(decimal price)
        {
            var result = validator.Validate(CreateCommand(price: price));

            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData(-0.01)]
        [InlineData(10000000000)]
        [InlineData(19.999)]
        public void Validate_WithAnInvalidPrice_ReturnsAPriceFailure(decimal price)
        {
            var result = validator.Validate(CreateCommand(price: price));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, failure => failure.PropertyName == "Price");
        }

        [Theory]
        [InlineData("")]
        [InlineData("us")]
        [InlineData("usd ")]
        [InlineData("USD")]
        [InlineData("us1")]
        public void Validate_WithAnInvalidCurrency_ReturnsACurrencyFailure(string currency)
        {
            var result = validator.Validate(CreateCommand(currency: currency));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, failure => failure.PropertyName == "Currency");
        }

        [Theory]
        [InlineData("Monthly")]
        [InlineData("Annual")]
        public void Validate_WithADefinedPeriod_IsValid(string period)
        {
            var result = validator.Validate(CreateCommand(period: period));

            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData("")]
        [InlineData("monthly")]
        [InlineData("Weekly")]
        public void Validate_WithAnUndefinedPeriod_ReturnsAPeriodFailure(string period)
        {
            var result = validator.Validate(CreateCommand(period: period));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, failure => failure.PropertyName == "Period");
        }

        [Fact]
        public void Validate_WithAnEmptyPlanId_ReturnsAPlanIdFailure()
        {
            var result = validator.Validate(CreateCommand(planId: Guid.Empty));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, failure => failure.PropertyName == "PlanId");
        }

        [Fact]
        public void Validate_WithNullEntitlements_ReturnsAnEntitlementsFailure()
        {
            var command = new CreatePlanVersionCommand(PlanId, 19.99m, "usd", "Monthly", null!);

            var result = validator.Validate(command);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, failure => failure.PropertyName == "Entitlements");
        }

        [Fact]
        public void Validate_WithAnEmptyEntitlementList_IsValid()
        {
            var result = validator.Validate(CreateCommand(entitlements: Array.Empty<PlanEntitlementRequest>()));

            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData("")]
        [InlineData("Max_tables")]
        [InlineData("1tables")]
        [InlineData("max tables")]
        public void Validate_WithAnInvalidEntitlementKey_ReturnsAFieldLevelFailure(string key)
        {
            var result = validator.Validate(CreateCommand(
                entitlements: new[] { new PlanEntitlementRequest(key, "10") }));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, failure => failure.PropertyName == "Entitlements[0].Key");
        }

        [Fact]
        public void Validate_WithAnOversizedEntitlementKeyOrValue_ReturnsFieldLevelFailures()
        {
            var result = validator.Validate(CreateCommand(
                entitlements: new[]
                {
                    new PlanEntitlementRequest("a" + new string('b', 128), "10"),
                    new PlanEntitlementRequest("max_tables", new string('v', 513)),
                }));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, failure => failure.PropertyName == "Entitlements[0].Key");
            Assert.Contains(result.Errors, failure => failure.PropertyName == "Entitlements[1].Value");
        }

        [Fact]
        public void Validate_WithTheMaximumEntitlementKeyAndValue_IsValid()
        {
            var result = validator.Validate(CreateCommand(
                entitlements: new[]
                {
                    new PlanEntitlementRequest("a" + new string('b', 127), new string('v', 512)),
                }));

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_WithADuplicateEntitlementKey_ReturnsAFieldLevelFailure()
        {
            var result = validator.Validate(CreateCommand(
                entitlements: new[]
                {
                    new PlanEntitlementRequest("max_tables", "10"),
                    new PlanEntitlementRequest("max_tables", "20"),
                }));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, failure => failure.PropertyName == "Entitlements[1].Key");
        }
    }
}
