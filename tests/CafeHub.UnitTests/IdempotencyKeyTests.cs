using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Idempotency;
using CafeHub.WebApi.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.UnitTests
{
    public sealed class IdempotencyKeyTests
    {
        private const string SixtyFourCharacterKey = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789ab";

        [Fact]
        public void TryCreate_AcceptsKeysAtTheDocumentedLengthBounds()
        {
            var shortest = IdempotencyKey.TryCreate("abcd1234");
            var longest = IdempotencyKey.TryCreate(SixtyFourCharacterKey);

            Assert.True(shortest.IsSuccess);
            Assert.Equal("abcd1234", shortest.Value.Value);
            Assert.True(longest.IsSuccess);
            Assert.Equal(SixtyFourCharacterKey, longest.Value.Value);
        }

        [Theory]
        [InlineData("abcd123")]
        [InlineData(SixtyFourCharacterKey + "c")]
        public void TryCreate_RejectsKeysOutsideTheLengthBounds(string invalidKey)
        {
            var result = IdempotencyKey.TryCreate(invalidKey);

            Assert.True(result.IsFailure);
            Assert.Equal(IdempotencyErrors.InvalidKeyCode, result.Error!.Code);
        }

        [Theory]
        [InlineData("abcd 123")]
        [InlineData("abcd+123")]
        [InlineData("abcd/123")]
        [InlineData("abcd=123")]
        [InlineData("abcd.123")]
        [InlineData("abcd:123")]
        [InlineData("abcd!123")]
        [InlineData("abcdé123")]
        [InlineData("abcd😀123")]
        [InlineData("abcd\t123")]
        public void TryCreate_RejectsDisallowedCharacters(string invalidKey)
        {
            var result = IdempotencyKey.TryCreate(invalidKey);

            Assert.True(result.IsFailure);
            Assert.Equal(IdempotencyErrors.InvalidKeyCode, result.Error!.Code);
            Assert.Equal(ErrorType.Validation, result.Error.Type);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void TryCreate_RejectsMissingOrEmptyKeys_WithTheValidationInvalidKeyError(string? invalidKey)
        {
            var result = IdempotencyKey.TryCreate(invalidKey);

            Assert.True(result.IsFailure);
            Assert.Equal(IdempotencyErrors.InvalidKeyCode, result.Error!.Code);
            Assert.Equal(IdempotencyErrors.InvalidKeyMessage, result.Error.Message);
        }

        [Fact]
        public void TryCreate_NeverEchoesTheSuppliedKey_InTheError()
        {
            const string secretLikeKey = "client secret value 1";

            var result = IdempotencyKey.TryCreate(secretLikeKey);

            Assert.True(result.IsFailure);
            Assert.DoesNotContain(secretLikeKey, result.Error!.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void TryCreate_AcceptsEveryAllowedCharacterClass()
        {
            Assert.True(IdempotencyKey.TryCreate("abcd-efgh").IsSuccess);
            Assert.True(IdempotencyKey.TryCreate("abcd_efgh").IsSuccess);
            Assert.True(IdempotencyKey.TryCreate("ABCD1234").IsSuccess);
        }

        [Fact]
        public void Keys_WithTheSameValue_AreEqual()
        {
            var first = IdempotencyKey.TryCreate("abcd1234").Value;
            var second = IdempotencyKey.TryCreate("abcd1234").Value;

            Assert.Equal(first, second);
            Assert.True(first.Equals(second));
        }

        [Fact]
        public void TheReuseError_IsAConflictWithTheDocumentedCode_AndMapsTo409()
        {
            var error = IdempotencyErrors.KeyReused();

            Assert.Equal(IdempotencyErrors.KeyReusedCode, error.Code);
            Assert.Equal(ErrorType.Conflict, error.Type);

            var problemDetails = ErrorProblemDetailsMapper.Map(error, "ctx-trace-idempotency");

            Assert.Equal(StatusCodes.Status409Conflict, problemDetails.Status);
            Assert.Equal(IdempotencyErrors.KeyReusedCode, problemDetails.Extensions["code"]);
            Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.5.10", problemDetails.Type);
        }
    }
}
