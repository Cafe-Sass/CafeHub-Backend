using CafeHub.Application.Staff.Commands;

namespace CafeHub.UnitTests.Staff.Commands
{
    public sealed class ResetStaffPasswordCommandValidatorTests
    {
        private readonly ResetStaffPasswordCommandValidator validator = new();
        private static readonly Guid StaffId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

        [Fact]
        public async Task Validate_ReturnsValid_ForWellFormedCommand()
        {
            var command = new ResetStaffPasswordCommand(StaffId, "NewPass123!");

            var result = await validator.ValidateAsync(command);

            Assert.True(result.IsValid);
        }

        [Fact]
        public async Task Validate_ReturnsInvalid_WhenStaffIdIsEmpty()
        {
            var command = new ResetStaffPasswordCommand(Guid.Empty, "NewPass123!");

            var result = await validator.ValidateAsync(command);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.PropertyName == "StaffId");
        }

        [Theory]
        [InlineData("")]
        [InlineData("12345")]
        public async Task Validate_ReturnsInvalid_WhenNewPasswordIsEmptyOrTooShort(string password)
        {
            var command = new ResetStaffPasswordCommand(StaffId, password);

            var result = await validator.ValidateAsync(command);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.PropertyName == "NewPassword");
        }
    }
}
