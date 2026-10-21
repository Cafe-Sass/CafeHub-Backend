using System.Diagnostics;
using CafeHub.Application.Abstractions.Results;

namespace CafeHub.UnitTests
{
    public sealed class ErrorTests
    {
        [Theory]
        [InlineData(ErrorType.Failure, null, "A human readable test message.")]
        [InlineData(ErrorType.Failure, "", "A human readable test message.")]
        [InlineData(ErrorType.Failure, "   ", "A human readable test message.")]
        [InlineData(ErrorType.Validation, null, "A human readable test message.")]
        [InlineData(ErrorType.Validation, "", "A human readable test message.")]
        [InlineData(ErrorType.Validation, "   ", "A human readable test message.")]
        [InlineData(ErrorType.NotFound, null, "A human readable test message.")]
        [InlineData(ErrorType.NotFound, "", "A human readable test message.")]
        [InlineData(ErrorType.NotFound, "   ", "A human readable test message.")]
        [InlineData(ErrorType.Conflict, null, "A human readable test message.")]
        [InlineData(ErrorType.Conflict, "", "A human readable test message.")]
        [InlineData(ErrorType.Conflict, "   ", "A human readable test message.")]
        [InlineData(ErrorType.Unauthorized, null, "A human readable test message.")]
        [InlineData(ErrorType.Unauthorized, "", "A human readable test message.")]
        [InlineData(ErrorType.Unauthorized, "   ", "A human readable test message.")]
        [InlineData(ErrorType.Forbidden, null, "A human readable test message.")]
        [InlineData(ErrorType.Forbidden, "", "A human readable test message.")]
        [InlineData(ErrorType.Forbidden, "   ", "A human readable test message.")]
        public void Factories_Throw_WhenCodeIsNullOrWhitespace(ErrorType errorType, string? code, string message)
        {
            var exception = Record.Exception(() => CreateErrorFor(errorType, code!, message));

            Assert.IsType<ArgumentException>(exception);
        }

        [Theory]
        [InlineData(ErrorType.Failure, "Test.Code", null)]
        [InlineData(ErrorType.Failure, "Test.Code", "")]
        [InlineData(ErrorType.Failure, "Test.Code", "   ")]
        [InlineData(ErrorType.Validation, "Test.Code", null)]
        [InlineData(ErrorType.Validation, "Test.Code", "")]
        [InlineData(ErrorType.Validation, "Test.Code", "   ")]
        [InlineData(ErrorType.NotFound, "Test.Code", null)]
        [InlineData(ErrorType.NotFound, "Test.Code", "")]
        [InlineData(ErrorType.NotFound, "Test.Code", "   ")]
        [InlineData(ErrorType.Conflict, "Test.Code", null)]
        [InlineData(ErrorType.Conflict, "Test.Code", "")]
        [InlineData(ErrorType.Conflict, "Test.Code", "   ")]
        [InlineData(ErrorType.Unauthorized, "Test.Code", null)]
        [InlineData(ErrorType.Unauthorized, "Test.Code", "")]
        [InlineData(ErrorType.Unauthorized, "Test.Code", "   ")]
        [InlineData(ErrorType.Forbidden, "Test.Code", null)]
        [InlineData(ErrorType.Forbidden, "Test.Code", "")]
        [InlineData(ErrorType.Forbidden, "Test.Code", "   ")]
        public void Factories_Throw_WhenMessageIsNullOrWhitespace(ErrorType errorType, string code, string? message)
        {
            var exception = Record.Exception(() => CreateErrorFor(errorType, code, message!));

            Assert.IsType<ArgumentException>(exception);
        }

        [Theory]
        [InlineData(ErrorType.Failure)]
        [InlineData(ErrorType.Validation)]
        [InlineData(ErrorType.NotFound)]
        [InlineData(ErrorType.Conflict)]
        [InlineData(ErrorType.Unauthorized)]
        [InlineData(ErrorType.Forbidden)]
        public void Factories_SetMatchingErrorType_AndRoundtripCodeAndMessage(ErrorType errorType)
        {
            var error = CreateErrorFor(errorType, "Test.Code", "A human readable test message.");

            Assert.Equal(errorType, error.Type);
            Assert.Equal("Test.Code", error.Code);
            Assert.Equal("A human readable test message.", error.Message);
        }

        private static Error CreateErrorFor(ErrorType errorType, string code, string message)
        {
            return errorType switch
            {
                ErrorType.Failure => Error.Failure(code, message),
                ErrorType.Validation => Error.Validation(code, message),
                ErrorType.NotFound => Error.NotFound(code, message),
                ErrorType.Conflict => Error.Conflict(code, message),
                ErrorType.Unauthorized => Error.Unauthorized(code, message),
                ErrorType.Forbidden => Error.Forbidden(code, message),
                var unrecognized => throw new UnreachableException($"An unhandled error type was used: {unrecognized}."),
            };
        }
    }
}
