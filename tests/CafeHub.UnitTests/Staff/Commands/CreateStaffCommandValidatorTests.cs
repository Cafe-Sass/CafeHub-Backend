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
            var command = new CreateStaffCommand("Jane Doe", "jane@test.com", "+966500000001", BranchId, "ExamplePass42!");

            var result = await validator.ValidateAsync(command);

            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Validate_ReturnsInvalid_WhenFullNameIsEmptyOrWhitespace(string name)
        {
            var command = new CreateStaffCommand(name, "jane@test.com", null, BranchId, "ExamplePass42!");

            var result = await validator.ValidateAsync(command);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.PropertyName == "FullName");
        }

        [Fact]
        public async Task Validate_ReturnsInvalid_WhenFullNameTooLong()
        {
            var command = new CreateStaffCommand(new string('A', CreateStaffCommandValidator.MaximumFullNameLength + 1), "jane@test.com", null, BranchId, "ExamplePass42!");

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
            var command = new CreateStaffCommand("Jane Doe", email, null, BranchId, "ExamplePass42!");

            var result = await validator.ValidateAsync(command);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.PropertyName == "Email");
        }

        [Fact]
        public async Task Validate_ReturnsInvalid_WhenBranchIdIsEmpty()
        {
            var command = new CreateStaffCommand("Jane Doe", "jane@test.com", null, Guid.Empty, "ExamplePass42!");

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

        [Theory]
        [InlineData(254, true)]
        [InlineData(255, false)]
        public async Task Validate_UsesTenantLoginEmailLimit(int length, bool valid)
        {
            const string domain = "@example.test";
            var email = new string('a', length - domain.Length) + domain;
            var result = await new CreateStaffCommandValidator().ValidateAsync(
                new CreateStaffCommand("Jane Doe", email, null, BranchId, "ExamplePass42!"));
            Assert.Equal(valid, result.IsValid);
        }

        [Fact]
        public async Task Validate_AllowsNullPhoneNumber()
        {
            var command = new CreateStaffCommand("Jane Doe", "jane@test.com", null, BranchId, "ExamplePass42!");

            var result = await validator.ValidateAsync(command);

            Assert.True(result.IsValid);
        }
    }
}
