using CafeHub.Application.Staff.Commands;

namespace CafeHub.UnitTests.Staff.Commands
{
    public sealed class CreateStaffCommandValidatorTests
    {
        private readonly CreateStaffCommandValidator validator = new();
        private static readonly Guid BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        [Fact]
        public async Task Validate_ReturnsValid_ForWellFormedCommand()
        {
            var command = new CreateStaffCommand("Jane Doe", "jane@test.com", "+966500000001", BranchId, "Secret123!");

            var result = await validator.ValidateAsync(command);

            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Validate_ReturnsInvalid_WhenFullNameIsEmptyOrWhitespace(string name)
        {
            var command = new CreateStaffCommand(name, "jane@test.com", null, BranchId, "Secret123!");

            var result = await validator.ValidateAsync(command);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.PropertyName == "FullName");
        }

        [Fact]
        public async Task Validate_ReturnsInvalid_WhenFullNameTooLong()
        {
            var command = new CreateStaffCommand(new string('A', CreateStaffCommandValidator.MaximumFullNameLength + 1), "jane@test.com", null, BranchId, "Secret123!");

            var result = await validator.ValidateAsync(command);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.PropertyName == "FullName");
        }

        [Theory]
        [InlineData("")]
        [InlineData("not-an-email")]
        [InlineData("missing@")]
        public async Task Validate_ReturnsInvalid_WhenEmailIsInvalid(string email)
        {
            var command = new CreateStaffCommand("Jane Doe", email, null, BranchId, "Secret123!");

            var result = await validator.ValidateAsync(command);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.PropertyName == "Email");
        }

        [Fact]
        public async Task Validate_ReturnsInvalid_WhenBranchIdIsEmpty()
        {
            var command = new CreateStaffCommand("Jane Doe", "jane@test.com", null, Guid.Empty, "Secret123!");

            var result = await validator.ValidateAsync(command);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.PropertyName == "BranchId");
        }

        [Theory]
        [InlineData("")]
        [InlineData("12345")]
        public async Task Validate_ReturnsInvalid_WhenPasswordIsEmptyOrTooShort(string password)
        {
            var command = new CreateStaffCommand("Jane Doe", "jane@test.com", null, BranchId, password);

            var result = await validator.ValidateAsync(command);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.PropertyName == "Password");
        }

        [Fact]
        public async Task Validate_AllowsNullPhoneNumber()
        {
            var command = new CreateStaffCommand("Jane Doe", "jane@test.com", null, BranchId, "Secret123!");

            var result = await validator.ValidateAsync(command);

            Assert.True(result.IsValid);
        }
    }
}
