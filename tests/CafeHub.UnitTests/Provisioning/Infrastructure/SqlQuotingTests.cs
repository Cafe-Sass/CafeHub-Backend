using CafeHub.Infrastructure.Provisioning;

namespace CafeHub.UnitTests.Provisioning.Infrastructure
{
    public sealed class SqlIdentifierTests
    {
        [Theory]
        [InlineData("cafehub_tenant_cool_cafe")]
        [InlineData("abc")]
        [InlineData("a")]
        [InlineData("role_1")]
        public void IsValid_AcceptsSnakeCaseIdentifiers(string identifier)
        {
            Assert.True(SqlIdentifier.IsValid(identifier));
        }

        [Theory]
        [InlineData("")]
        [InlineData("Bad_Name")]
        [InlineData("1abc")]
        [InlineData("has-dash")]
        [InlineData("has space")]
        [InlineData("has\"quote")]
        [InlineData("has;semicolon")]
        [InlineData("tab\there")]
        public void IsValid_RejectsUnsafeIdentifiers(string identifier)
        {
            Assert.False(SqlIdentifier.IsValid(identifier));
        }

        [Fact]
        public void Quote_WrapsAValidIdentifierInDoubleQuotes()
        {
            Assert.Equal("\"cafehub_tenant_cool_cafe\"", SqlIdentifier.Quote("cafehub_tenant_cool_cafe"));
        }

        [Fact]
        public void Quote_RejectsAnUnsafeIdentifier_WithoutBuildingSql()
        {
            Assert.Throws<ArgumentException>(() => SqlIdentifier.Quote("has\"quote"));
        }

        [Fact]
        public void IsValid_RejectsIdentifiersLongerThanSixtyThreeCharacters()
        {
            Assert.False(SqlIdentifier.IsValid(new string('a', 64)));
        }
    }

    public sealed class SqlLiteralTests
    {
        [Fact]
        public void Quote_WrapsAValueInSingleQuotes()
        {
            Assert.Equal("'value'", SqlLiteral.Quote("value"));
        }

        [Fact]
        public void Quote_EscapesSingleQuotesByDoublingThem()
        {
            Assert.Equal("'o''brien'", SqlLiteral.Quote("o'brien"));
        }

        [Fact]
        public void Quote_RejectsControlCharacters()
        {
            Assert.Throws<ArgumentException>(() => SqlLiteral.Quote("bad\nvalue"));
            Assert.Throws<ArgumentException>(() => SqlLiteral.Quote("bad\u0001value"));
        }

        [Fact]
        public void Quote_RejectsNull()
        {
            Assert.Throws<ArgumentException>(() => SqlLiteral.Quote(null!));
        }
    }
}
