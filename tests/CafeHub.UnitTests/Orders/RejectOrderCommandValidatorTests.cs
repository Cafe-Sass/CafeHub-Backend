using CafeHub.Application.Orders.Commands;
using FluentValidation.Results;

namespace CafeHub.UnitTests.Orders
{
    public sealed class RejectOrderCommandValidatorTests
    {
        private readonly RejectOrderCommandValidator validator = new();
        private readonly AcceptOrderCommandValidator acceptValidator = new();

        [Fact]
        public void AWellFormedRejectionPasses()
        {
            Assert.True(Validate(new RejectOrderCommand(Guid.CreateVersion7(), "Out of oat milk")).IsValid);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("ab")]
        public void AReasonShorterThanThreeTrimmedCharactersIsRefused(string? reason)
        {
            Assert.False(Validate(new RejectOrderCommand(Guid.CreateVersion7(), reason!)).IsValid);
        }

        [Theory]
        [InlineData("abc")]
        [InlineData(" abc ")]
        public void ThreeTrimmedCharactersPass(string reason)
        {
            Assert.True(Validate(new RejectOrderCommand(Guid.CreateVersion7(), reason)).IsValid);
        }

        [Fact]
        public void TheMaximumReasonIsTwoHundredFiftySixTrimmedCharacters()
        {
            Assert.True(Validate(new RejectOrderCommand(Guid.CreateVersion7(), new string('a', 256))).IsValid);
            Assert.True(Validate(new RejectOrderCommand(Guid.CreateVersion7(), $"  {new string('a', 256)}  ")).IsValid);
            Assert.False(Validate(new RejectOrderCommand(Guid.CreateVersion7(), new string('a', 257))).IsValid);
        }

        [Fact]
        public void ControlCharactersInsideTheReasonAreRefused()
        {
            Assert.False(Validate(new RejectOrderCommand(Guid.CreateVersion7(), "a\tb")).IsValid);
        }

        [Fact]
        public void AnEmptyOrderIdIsRefused()
        {
            Assert.False(Validate(new RejectOrderCommand(Guid.Empty, "Out of oat milk")).IsValid);
        }

        [Fact]
        public void TheAcceptCommandNeedsNoReason()
        {
            Assert.True(acceptValidator.Validate(new AcceptOrderCommand(Guid.CreateVersion7())).IsValid);
            Assert.False(acceptValidator.Validate(new AcceptOrderCommand(Guid.Empty)).IsValid);
        }

        private ValidationResult Validate(RejectOrderCommand command) => validator.Validate(command);
    }
}
