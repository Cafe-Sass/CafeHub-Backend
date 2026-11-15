using CafeHub.Application.Staff.Commands;

namespace CafeHub.UnitTests.Staff.Commands
{
    public sealed class ChangePasswordCommandValidatorTests
    {
        private readonly ChangePasswordCommandValidator validator = new();

        [Fact]
        public async Task Validate_ReturnsValid_ForWellFormedCommand()
        {
            var command = new ChangePasswordCommand("OldPass123!", "NewPass456!");

            var result = await validator.ValidateAsync(command);

            Assert.True(result.IsValid);
        }

        [Fact]
        public async Task Validate_ReturnsInvalid_WhenCurrentPasswordIsEmpty()
        {
            var command = new ChangePasswordCommand("", "NewPass456!");

            var result = await validator.ValidateAsync(command);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.PropertyName == "CurrentPassword");
        }

        [Theory]
        [InlineData("")]
        [InlineData("12345")]
        public async Task Validate_ReturnsInvalid_WhenNewPasswordIsEmptyOrTooShort(string newPassword)
        {
            var command = new ChangePasswordCommand("OldPass123!", newPassword);

            var result = await validator.ValidateAsync(command);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.PropertyName == "NewPassword");
        }

        [Fact]
        public async Task Validate_ReturnsInvalid_WhenBothPasswordsAreEmpty()
        {
            var command = new ChangePasswordCommand("", "");

            var result = await validator.ValidateAsync(command);

            Assert.False(result.IsValid);
            Assert.True(result.Errors.Count >= 2);
        }
    }
}
