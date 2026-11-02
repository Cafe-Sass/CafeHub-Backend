using CafeHub.Application.Branches.Commands;

namespace CafeHub.UnitTests.Branches.Commands
{
    public sealed class UpdateBranchCommandValidatorTests
    {
        private readonly UpdateBranchCommandValidator validator = new();

        private static UpdateBranchCommand Command(
            Guid? branchId = null,
            string name = "Downtown Cafe",
            string? address = "123 Main St",
            string? contactPhone = "+1-555-0100",
            string? openingHoursSummary = "Mon-Fri 8am-6pm",
            string concurrencyToken = "12345")
        {
            return new(branchId ?? Guid.NewGuid(), name, address, contactPhone, openingHoursSummary, concurrencyToken);
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
        public void Validate_WithEmptyOrWhitespaceName_IsInvalid(string name)
        {
            var result = validator.Validate(Command(name: name));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, failure => failure.PropertyName == "Name");
        }

        [Fact]
        public void Validate_WithNameAtMaximumLength_IsValid()
        {
            var result = validator.Validate(Command(name: new string('a', UpdateBranchCommandValidator.MaximumNameLength)));

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_WithNameAboveMaximumLength_IsInvalid()
        {
            var result = validator.Validate(Command(name: new string('a', UpdateBranchCommandValidator.MaximumNameLength + 1)));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, failure => failure.PropertyName == "Name");
        }

        [Fact]
        public void Validate_WithAddressAtMaximumLength_IsValid()
        {
            var result = validator.Validate(Command(address: new string('x', UpdateBranchCommandValidator.MaximumAddressLength)));

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_WithAddressAboveMaximumLength_IsInvalid()
        {
            var result = validator.Validate(Command(address: new string('x', UpdateBranchCommandValidator.MaximumAddressLength + 1)));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, failure => failure.PropertyName == "Address");
        }

        [Fact]
        public void Validate_WithContactPhoneAtMaximumLength_IsValid()
        {
            var result = validator.Validate(Command(contactPhone: new string('5', UpdateBranchCommandValidator.MaximumContactPhoneLength)));

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_WithContactPhoneAboveMaximumLength_IsInvalid()
        {
            var result = validator.Validate(Command(contactPhone: new string('5', UpdateBranchCommandValidator.MaximumContactPhoneLength + 1)));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, failure => failure.PropertyName == "ContactPhone");
        }

        [Fact]
        public void Validate_WithOpeningHoursSummaryAtMaximumLength_IsValid()
        {
            var result = validator.Validate(Command(openingHoursSummary: new string('x', UpdateBranchCommandValidator.MaximumOpeningHoursSummaryLength)));

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_WithOpeningHoursSummaryAboveMaximumLength_IsInvalid()
        {
            var result = validator.Validate(Command(openingHoursSummary: new string('x', UpdateBranchCommandValidator.MaximumOpeningHoursSummaryLength + 1)));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, failure => failure.PropertyName == "OpeningHoursSummary");
        }

        [Theory]
        [InlineData(null, null, null)]
        [InlineData("", "", "")]
        [InlineData("   ", "   ", "   ")]
        public void Validate_WithEmptyOptionalFields_IsValid(string? address, string? contactPhone, string? openingHoursSummary)
        {
            var result = validator.Validate(Command(address: address, contactPhone: contactPhone, openingHoursSummary: openingHoursSummary));

            Assert.True(result.IsValid);
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
