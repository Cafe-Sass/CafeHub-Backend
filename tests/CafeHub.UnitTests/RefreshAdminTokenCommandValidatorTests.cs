using CafeHub.Application.Identity;
using CafeHub.Application.Identity.Commands;

namespace CafeHub.UnitTests
{
    public sealed class RefreshAdminTokenCommandValidatorTests
    {
        private readonly RefreshAdminTokenCommandValidator validator = new();

        [Fact]
        public void AcceptsATokenAtTheMaximumLength()
        {
            var token = new string('x', IRefreshTokenService.MaxRawTokenLength);

            var result = validator.Validate(new RefreshAdminTokenCommand(token));

            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void RejectsAnEmptyToken(string token)
        {
            var result = validator.Validate(new RefreshAdminTokenCommand(token));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error => error.PropertyName == "RefreshToken");
        }

        [Fact]
        public void RejectsATokenLongerThanTheMaximumLength()
        {
            var token = new string('x', IRefreshTokenService.MaxRawTokenLength + 1);

            var result = validator.Validate(new RefreshAdminTokenCommand(token));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error => error.PropertyName == "RefreshToken");
        }

        [Fact]
        public void TheOverlengthTokenMessage_NeverEchoesTheValue()
        {
            const string secretTail = "secret-tail-77a1";
            var token = new string('x', IRefreshTokenService.MaxRawTokenLength) + secretTail;

            var result = validator.Validate(new RefreshAdminTokenCommand(token));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error => error.PropertyName == "RefreshToken");
            Assert.All(result.Errors, error => Assert.DoesNotContain(secretTail, error.ErrorMessage, StringComparison.Ordinal));
        }
    }
}
