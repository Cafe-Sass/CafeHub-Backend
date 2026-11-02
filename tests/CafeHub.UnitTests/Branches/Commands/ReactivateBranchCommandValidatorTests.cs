using CafeHub.Application.Branches.Commands;

namespace CafeHub.UnitTests.Branches.Commands
{
    public sealed class ReactivateBranchCommandValidatorTests
    {
        private readonly ReactivateBranchCommandValidator validator = new();

        private static ReactivateBranchCommand Command(Guid? branchId = null, string concurrencyToken = "12345")
        {
            return new(branchId ?? Guid.NewGuid(), concurrencyToken);
        }

        [Fact]
        public void Validate_WithValidCommand_IsValid()
        {
            var result = validator.Validate(Command());

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_WithEmptyBranchId_IsInvalid()
        {
            var result = validator.Validate(Command(branchId: Guid.Empty));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, failure => failure.PropertyName == "BranchId");
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("abc")]
        [InlineData("4294967296")]
        public void Validate_WithInvalidConcurrencyToken_ReturnsFieldError(string concurrencyToken)
        {
            var result = validator.Validate(Command(concurrencyToken: concurrencyToken));

            Assert.False(result.IsValid);
            Assert.Contains(
                result.Errors,
                failure => failure.PropertyName == "ConcurrencyToken"
                    && failure.ErrorMessage == "The concurrency token is not a valid unsigned integer.");
        }
    }
}
