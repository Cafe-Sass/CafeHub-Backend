using CafeHub.Application.Plans.Commands;

namespace CafeHub.UnitTests.Plans.Commands
{
    public sealed class CreatePlanCommandValidatorTests
    {
        private readonly CreatePlanCommandValidator validator = new();

        [Fact]
        public void Validate_WithAValidCommand_IsValid()
        {
            var result = validator.Validate(new CreatePlanCommand("starter", "Starter", true));

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_WithTheMinimumKeyAndMaximumName_IsValid()
        {
            var result = validator.Validate(new CreatePlanCommand("abc", new string('n', 256), true));

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_WithTheMaximumKey_IsValid()
        {
            var key = "a" + new string('b', 63);

            var result = validator.Validate(new CreatePlanCommand(key, "Starter", true));

            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData("")]
        [InlineData("ab")]
        [InlineData("Starter")]
        [InlineData("1starter")]
        [InlineData("starter_plan")]
        [InlineData("starter plan")]
        public void Validate_WithAnInvalidKey_ReturnsAKeyFailure(string key)
        {
            var result = validator.Validate(new CreatePlanCommand(key, "Starter", true));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, failure => failure.PropertyName == "Key");
        }

        [Fact]
        public void Validate_WithAKeyLongerThanTheMaximum_ReturnsAKeyFailure()
        {
            var key = "a" + new string('b', 64);

            var result = validator.Validate(new CreatePlanCommand(key, "Starter", true));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, failure => failure.PropertyName == "Key");
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Validate_WithAnEmptyName_ReturnsANameFailure(string name)
        {
            var result = validator.Validate(new CreatePlanCommand("starter", name, true));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, failure => failure.PropertyName == "Name");
        }

        [Fact]
        public void Validate_WithANameLongerThanTheMaximum_ReturnsANameFailure()
        {
            var result = validator.Validate(new CreatePlanCommand("starter", new string('n', 257), true));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, failure => failure.PropertyName == "Name");
        }
    }
}
