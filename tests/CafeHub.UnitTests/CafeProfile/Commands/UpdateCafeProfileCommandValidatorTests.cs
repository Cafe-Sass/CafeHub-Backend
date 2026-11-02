using CafeHub.Application.CafeProfile.Commands;

namespace CafeHub.UnitTests.CafeProfile.Commands
{
    public sealed class UpdateCafeProfileCommandValidatorTests
    {
        private readonly UpdateCafeProfileCommandValidator validator = new();

        private static UpdateCafeProfileCommand Command(
            string cafeName = "My Cafe",
            string? logoMediaReference = "media-ref-123",
            string? primaryColor = "#ff0000",
            string? secondaryColor = "#00ff00",
            string? accentColor = "#0000ff",
            string? contactPhone = "+1-555-0100",
            string? contactEmail = "contact@example.com",
            string? contactAddress = "123 Main St",
            string timezone = "America/New_York",
            string concurrencyToken = "12345")
        {
            return new(
                cafeName,
                logoMediaReference,
                primaryColor,
                secondaryColor,
                accentColor,
                contactPhone,
                contactEmail,
                contactAddress,
                timezone,
                concurrencyToken);
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
        public void Validate_WithEmptyOrWhitespaceCafeName_IsInvalid(string cafeName)
        {
            var result = validator.Validate(Command(cafeName: cafeName));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, failure => failure.PropertyName == "CafeName");
        }

        [Fact]
        public void Validate_WithCafeNameAtMaximumLength_IsValid()
        {
            var result = validator.Validate(Command(cafeName: new string('a', UpdateCafeProfileCommandValidator.MaximumNameLength)));

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_WithCafeNameAboveMaximumLength_IsInvalid()
        {
            var result = validator.Validate(Command(cafeName: new string('a', UpdateCafeProfileCommandValidator.MaximumNameLength + 1)));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, failure => failure.PropertyName == "CafeName");
        }

        [Theory]
        [InlineData("#a1B2c3", "#FF0000", "#000000")]
        public void Validate_WithWellFormedColors_IsValid(string primaryColor, string secondaryColor, string accentColor)
        {
            var result = validator.Validate(Command(primaryColor: primaryColor, secondaryColor: secondaryColor, accentColor: accentColor));

            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData("a1b2c3")]
        [InlineData("#12345")]
        [InlineData("#1234567")]
        [InlineData("#GGGGGG")]
        public void Validate_WithMalformedPrimaryColor_IsInvalid(string primaryColor)
        {
            var result = validator.Validate(Command(primaryColor: primaryColor));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, failure => failure.PropertyName == "PrimaryColor");
        }

        [Fact]
        public void Validate_WithLogoAtMaximumLength_IsValid()
        {
            var result = validator.Validate(Command(logoMediaReference: new string('x', UpdateCafeProfileCommandValidator.MaximumLogoMediaReferenceLength)));

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_WithLogoAboveMaximumLength_IsInvalid()
        {
            var result = validator.Validate(Command(logoMediaReference: new string('x', UpdateCafeProfileCommandValidator.MaximumLogoMediaReferenceLength + 1)));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, failure => failure.PropertyName == "LogoMediaReference");
        }

        [Theory]
        [InlineData("+1-555-0100")]
        [InlineData("(555) 0100")]
        [InlineData("5550100")]
        public void Validate_WithValidPhone_IsValid(string phone)
        {
            var result = validator.Validate(Command(contactPhone: phone));

            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData("!!!")]
        [InlineData("@@@")]
        public void Validate_WithInvalidPhone_IsInvalid(string phone)
        {
            var result = validator.Validate(Command(contactPhone: phone));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, failure => failure.PropertyName == "ContactPhone");
        }

        [Theory]
        [InlineData("not-an-email")]
        [InlineData("user@")]
        [InlineData("@example.com")]
        public void Validate_WithMalformedEmail_IsInvalid(string email)
        {
            var result = validator.Validate(Command(contactEmail: email));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, failure => failure.PropertyName == "ContactEmail");
        }

        [Fact]
        public void Validate_WithEmailAboveMaximumLength_IsInvalid()
        {
            var result = validator.Validate(Command(contactEmail: new string('a', 321) + "@example.com"));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, failure => failure.PropertyName == "ContactEmail");
        }

        [Fact]
        public void Validate_WithAddressAtMaximumLength_IsValid()
        {
            var result = validator.Validate(Command(contactAddress: new string('x', UpdateCafeProfileCommandValidator.MaximumAddressLength)));

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_WithAddressAboveMaximumLength_IsInvalid()
        {
            var result = validator.Validate(Command(contactAddress: new string('x', UpdateCafeProfileCommandValidator.MaximumAddressLength + 1)));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, failure => failure.PropertyName == "ContactAddress");
        }

        [Theory]
        [InlineData("Africa/Cairo")]
        [InlineData("America/New_York")]
        [InlineData("UTC")]
        public void Validate_WithValidTimeZone_IsValid(string timezone)
        {
            var result = validator.Validate(Command(timezone: timezone));

            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("Not/AZone")]
        public void Validate_WithInvalidTimeZone_IsInvalid(string timezone)
        {
            var result = validator.Validate(Command(timezone: timezone));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, failure => failure.PropertyName == "Timezone");
        }

        [Fact]
        public void Validate_WithTimeZoneAboveMaximumLength_IsInvalid()
        {
            var result = validator.Validate(Command(timezone: new string('a', UpdateCafeProfileCommandValidator.MaximumTimezoneLength + 1)));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, failure => failure.PropertyName == "Timezone");
        }

        [Theory]
        [InlineData("0")]
        [InlineData("4294967295")]
        public void Validate_WithValidConcurrencyToken_IsValid(string concurrencyToken)
        {
            var result = validator.Validate(Command(concurrencyToken: concurrencyToken));

            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("abc")]
        [InlineData("4294967296")]
        public void Validate_WithInvalidConcurrencyToken_IsInvalid(string concurrencyToken)
        {
            var result = validator.Validate(Command(concurrencyToken: concurrencyToken));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, failure => failure.PropertyName == "ConcurrencyToken");
        }
    }
}
