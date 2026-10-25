using CafeHub.Application.Plans.Queries;

namespace CafeHub.UnitTests.Plans.Queries
{
    public sealed class GetPlanQueryValidatorTests
    {
        private readonly GetPlanQueryValidator validator = new();

        [Fact]
        public void Validate_WithAPlanId_IsValid()
        {
            var result = validator.Validate(new GetPlanQuery(Guid.NewGuid()));

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_WithAnEmptyPlanId_ReturnsAPlanIdFailure()
        {
            var result = validator.Validate(new GetPlanQuery(Guid.Empty));

            Assert.False(result.IsValid);
            var failure = Assert.Single(result.Errors);
            Assert.Equal("PlanId", failure.PropertyName);
        }
    }
}
