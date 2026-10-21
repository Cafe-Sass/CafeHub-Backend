using CafeHub.Application.Abstractions.Results;

namespace CafeHub.UnitTests
{
    public sealed class ResultTests
    {
        [Fact]
        public void Success_HasNoError_AndReportsIsSuccess()
        {
            var result = Result.Success();

            Assert.True(result.IsSuccess);
            Assert.False(result.IsFailure);
            Assert.Null(result.Error);
        }

        [Fact]
        public void Failure_CarriesError_AndReportsIsFailure()
        {
            var error = Error.Failure("Test.Failed", "A human readable test message.");

            var result = Result.Failure(error);

            Assert.True(result.IsFailure);
            Assert.False(result.IsSuccess);
            Assert.Same(error, result.Error);
        }

        [Fact]
        public void Failure_Throws_WhenErrorIsNull()
        {
            var exception = Record.Exception(() => Result.Failure(null!));

            Assert.IsType<ArgumentNullException>(exception);
        }

        [Fact]
        public void Value_ReturnsTheSuccessValue()
        {
            var result = Result.Success("cafe-alpha");

            Assert.True(result.IsSuccess);
            Assert.Equal("cafe-alpha", result.Value);
        }

        [Fact]
        public void Value_Throws_WhenResultIsFailed()
        {
            var result = Result.Failure<string>(Error.NotFound("Test.NotFound", "A human readable test message."));

            var exception = Record.Exception(() => _ = result.Value);

            Assert.IsType<InvalidOperationException>(exception);
        }
    }
}
