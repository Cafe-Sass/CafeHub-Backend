using CafeHub.Application.Identity;

namespace CafeHub.UnitTests
{
    public sealed class NewPasswordPolicyTests
    {
        [Theory]
        [InlineData(11, false)]
        [InlineData(12, true)]
        [InlineData(128, true)]
        [InlineData(129, false)]
        public void EnforcesLength(int length, bool valid) =>
            Assert.Equal(valid, NewPasswordPolicy.IsValid("Aa1!" + new string('a', length - 4)));

        [Theory]
        [InlineData("no-uppercase-1!")]
        [InlineData("NO-LOWERCASE-1!")]
        [InlineData("NoDigitAtAll!")]
        [InlineData("NoSpecialHere1 ")]
        public void RequiresEachCharacterClass(string value) => Assert.False(NewPasswordPolicy.IsValid(value));

        [Fact]
        public void AcceptsUnicodeLetterDigitAndSymbol() =>
            Assert.True(NewPasswordPolicy.IsValid("Äé٣€abcdefgh"));
    }
}
