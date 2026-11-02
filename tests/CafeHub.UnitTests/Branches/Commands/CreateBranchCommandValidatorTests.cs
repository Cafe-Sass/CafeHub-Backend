using CafeHub.Application.Branches.Commands;

namespace CafeHub.UnitTests.Branches.Commands
{
    public sealed class CreateBranchCommandValidatorTests
    {
        private readonly CreateBranchCommandValidator validator = new();

        private static CreateBranchCommand Command(
            string name = "Downtown Cafe",
            string? address = "123 Main St",
            string? contactPhone = "+1-555-0100",
            string? openingHoursSummary = "Mon-Fri 8am-6pm")
        {
            return new(name, address, contactPhone, openingHoursSummary);
        }

        [Fact]
        public void Validate_WithValidCommand_IsValid()
        {
            var result = validator.Validate(Command());

            Assert.True(result.IsValid);
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
        public void Validate_WithNameOfOneCharacter_IsValid()
        {
            var result = validator.Validate(Command(name: "a"));

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_WithNameAtMaximumLength_IsValid()
        {
            var result = validator.Validate(Command(name: new string('a', CreateBranchCommandValidator.MaximumNameLength)));

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_WithNameAboveMaximumLength_IsInvalid()
        {
            var result = validator.Validate(Command(name: new string('a', CreateBranchCommandValidator.MaximumNameLength + 1)));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, failure => failure.PropertyName == "Name");
        }

        [Fact]
        public void Validate_WithAddressAtMaximumLength_IsValid()
        {
            var result = validator.Validate(Command(address: new string('x', CreateBranchCommandValidator.MaximumAddressLength)));

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_WithAddressAboveMaximumLength_IsInvalid()
        {
            var result = validator.Validate(Command(address: new string('x', CreateBranchCommandValidator.MaximumAddressLength + 1)));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, failure => failure.PropertyName == "Address");
        }

        [Fact]
        public void Validate_WithContactPhoneAtMaximumLength_IsValid()
        {
            var result = validator.Validate(Command(contactPhone: new string('5', CreateBranchCommandValidator.MaximumContactPhoneLength)));

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_WithContactPhoneAboveMaximumLength_IsInvalid()
        {
            var result = validator.Validate(Command(contactPhone: new string('5', CreateBranchCommandValidator.MaximumContactPhoneLength + 1)));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, failure => failure.PropertyName == "ContactPhone");
        }

        [Fact]
        public void Validate_WithOpeningHoursSummaryAtMaximumLength_IsValid()
        {
            var result = validator.Validate(Command(openingHoursSummary: new string('x', CreateBranchCommandValidator.MaximumOpeningHoursSummaryLength)));

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_WithOpeningHoursSummaryAboveMaximumLength_IsInvalid()
        {
            var result = validator.Validate(Command(openingHoursSummary: new string('x', CreateBranchCommandValidator.MaximumOpeningHoursSummaryLength + 1)));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, failure => failure.PropertyName == "OpeningHoursSummary");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Validate_WithEmptyOptionalAddress_IsValid(string? address)
        {
            var result = validator.Validate(Command(address: address));

            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Validate_WithEmptyOptionalContactPhone_IsValid(string? contactPhone)
        {
            var result = validator.Validate(Command(contactPhone: contactPhone));

            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Validate_WithEmptyOptionalOpeningHoursSummary_IsValid(string? openingHoursSummary)
        {
            var result = validator.Validate(Command(openingHoursSummary: openingHoursSummary));

            Assert.True(result.IsValid);
        }
    }
}
