using CafeHub.Application.Identity.Commands;

namespace CafeHub.UnitTests
{
    public sealed class LoginAdminCommandValidatorTests
    {
        private const string ValidEmail = "super.admin@cafehub.app";
        private const string ValidPassword = "known-password-9f2f7d61";

        private readonly LoginAdminCommandValidator validator = new();

        [Fact]
        public void AcceptsAValidEmailAndPassword()
        {
            var result = validator.Validate(new LoginAdminCommand(ValidEmail, ValidPassword));

            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void RejectsAnEmptyEmail(string email)
        {
            var result = validator.Validate(new LoginAdminCommand(email, ValidPassword));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error => error.PropertyName == "Email");
        }

        [Theory]
        [InlineData("plain-string")]
        [InlineData("another-plain-string")]
        public void RejectsAnEmailWithoutAValidAddress(string email)
        {
            var result = validator.Validate(new LoginAdminCommand(email, ValidPassword));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error => error.PropertyName == "Email");
        }

        [Fact]
        public void RejectsAnEmailLongerThanTheMaximumLength()
        {
            var email = $"{new string('a', LoginAdminCommandValidator.MaxEmailLength - 11)}@cafehub.app";

            var result = validator.Validate(new LoginAdminCommand(email, ValidPassword));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error => error.PropertyName == "Email");
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void RejectsAnEmptyPassword(string password)
        {
            var result = validator.Validate(new LoginAdminCommand(ValidEmail, password));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error => error.PropertyName == "Password");
        }

        [Fact]
        public void RejectsAPasswordLongerThanTheMaximumLength()
        {
            var password = new string('x', LoginAdminCommandValidator.MaxPasswordLength) + "l";

            var result = validator.Validate(new LoginAdminCommand(ValidEmail, password));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error => error.PropertyName == "Password");
        }

        [Theory]
        [InlineData("plain-string")]
        [InlineData("another-plain-string")]
        public void ValidationMessages_NeverEchoTheSubmittedEmail(string email)
        {
            var result = validator.Validate(new LoginAdminCommand(email, ValidPassword));

            Assert.False(result.IsValid);
            Assert.All(result.Errors, error => Assert.DoesNotContain(email, error.ErrorMessage, StringComparison.Ordinal));
        }

        [Fact]
        public void ValidationMessages_NeverEchoTheSubmittedPassword()
        {
            const string password = "wishes-were-fishes-42";

            var result = validator.Validate(new LoginAdminCommand("plain-string", password));

            Assert.False(result.IsValid);
            Assert.All(result.Errors, error => Assert.DoesNotContain(password, error.ErrorMessage, StringComparison.Ordinal));
        }

        [Fact]
        public void TheOverlengthPasswordMessage_NeverEchoesTheValue()
        {
            const string secretTail = "secret-tail-77a1";
            var password = new string('x', LoginAdminCommandValidator.MaxPasswordLength) + secretTail;

            var result = validator.Validate(new LoginAdminCommand(ValidEmail, password));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error => error.PropertyName == "Password");
            Assert.All(result.Errors, error => Assert.DoesNotContain(secretTail, error.ErrorMessage, StringComparison.Ordinal));
        }
    }
}
