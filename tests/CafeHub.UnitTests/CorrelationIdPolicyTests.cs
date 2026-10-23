using CafeHub.WebApi.Correlation;

namespace CafeHub.UnitTests
{
    public sealed class CorrelationIdPolicyTests
    {
        public static TheoryData<string?> InvalidValues => new()
        {
            { null },
            { string.Empty },
            { "   " },
            { "cafe hub" },
            { "cafe/hub" },
            { "cafe\x001Fhub" },
            { "caféhub" },
            { "cafe\"hub" },
            { "cafe\nhub" },
        };

        public static TheoryData<string> ValidValues => new()
        {
            { "a" },
            { "Z" },
            { "0" },
            { "9" },
            { "-" },
            { "_" },
            { "." },
            { ":" },
            { new string('a', CorrelationIdPolicy.MaximumLength) },
        };

        [Fact]
        public void IsValid_RejectsValuesAboveTheMaximumLength()
        {
            Assert.False(CorrelationIdPolicy.IsValid(new string('a', CorrelationIdPolicy.MaximumLength + 1)));
        }

        [Theory]
        [MemberData(nameof(ValidValues))]
        public void IsValid_AcceptsEveryAllowedCharacterClass_AndTheMaximumLength(string validValue)
        {
            Assert.True(CorrelationIdPolicy.IsValid(validValue));
        }

        [Theory]
        [MemberData(nameof(InvalidValues))]
        public void IsValid_RejectsMissing_WhitespaceOnly_AndEveryDisallowedCharacterValue(string? invalidValue)
        {
            Assert.False(CorrelationIdPolicy.IsValid(invalidValue));
        }
    }
}
