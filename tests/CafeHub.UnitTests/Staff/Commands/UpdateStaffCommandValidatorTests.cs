using CafeHub.Application.Staff.Commands;

namespace CafeHub.UnitTests.Staff.Commands
{
    public sealed class UpdateStaffCommandValidatorTests
    {
        private readonly UpdateStaffCommandValidator validator = new();
        private static readonly Guid StaffId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        private static readonly Guid BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        [Fact]
        public async Task Validate_ReturnsValid_ForWellFormedCommand()
        {
            var command = new UpdateStaffCommand(StaffId, "Jane Smith", null, BranchId, "12345");

            var result = await validator.ValidateAsync(command);

            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Validate_ReturnsInvalid_WhenFullNameIsEmptyOrWhitespace(string name)
        {
            var command = new UpdateStaffCommand(StaffId, name, null, BranchId, "12345");

            var result = await validator.ValidateAsync(command);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.PropertyName == "FullName");
        }

        [Fact]
        public async Task Validate_ReturnsInvalid_WhenFullNameTooLong()
        {
            var command = new UpdateStaffCommand(StaffId, new string('A', UpdateStaffCommandValidator.MaximumFullNameLength + 1), null, BranchId, "12345");

            var result = await validator.ValidateAsync(command);

            Assert.False(result.IsValid);
        }

        [Fact]
        public async Task Validate_ReturnsInvalid_WhenStaffIdIsEmpty()
        {
            var command = new UpdateStaffCommand(Guid.Empty, "Jane Smith", null, BranchId, "12345");

            var result = await validator.ValidateAsync(command);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.PropertyName == "StaffId");
        }

        [Fact]
        public async Task Validate_ReturnsInvalid_WhenBranchIdIsEmpty()
        {
            var command = new UpdateStaffCommand(StaffId, "Jane Smith", null, Guid.Empty, "12345");

            var result = await validator.ValidateAsync(command);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.PropertyName == "BranchId");
        }

        [Fact]
        public async Task Validate_ReturnsInvalid_WhenConcurrencyTokenIsEmpty()
        {
            var command = new UpdateStaffCommand(StaffId, "Jane Smith", null, BranchId, "");

            var result = await validator.ValidateAsync(command);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.PropertyName == "ConcurrencyToken");
        }
    }
}
