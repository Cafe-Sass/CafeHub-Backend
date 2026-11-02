using CafeHub.Application.Branches.Queries;

namespace CafeHub.UnitTests.Branches.Queries
{
    public sealed class GetBranchQueryValidatorTests
    {
        private readonly GetBranchQueryValidator validator = new();

        [Fact]
        public void Validate_WithAValidId_IsValid()
        {
            var result = validator.Validate(new GetBranchQuery(Guid.NewGuid()));

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_WithAnEmptyId_IsInvalid()
        {
            var result = validator.Validate(new GetBranchQuery(Guid.Empty));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, failure => failure.PropertyName == "BranchId");
        }
    }
}
