using CafeHub.Domain.Onboarding;

namespace CafeHub.UnitTests.Onboarding.Domain
{
    public sealed class TenantSlugTests
    {
        [Theory]
        [InlineData("abc")]
        [InlineData("a1b2c3")]
        [InlineData("my-cafe")]
        [InlineData("a-b")]
        [InlineData("cafe-1")]
        public void Create_AcceptsValidSlug(string slug)
        {
            var result = TenantSlug.Create(slug);

            Assert.Equal(slug, result.Value);
        }

        [Fact]
        public void Create_AcceptsMinimumLengthSlug()
        {
            var result = TenantSlug.Create("abc");

            Assert.Equal("abc", result.Value);
        }

        [Fact]
        public void Create_AcceptsMaximumLengthSlug()
        {
            var slug = "a" + new string('b', 38) + "z";

            var result = TenantSlug.Create(slug);

            Assert.Equal(40, result.Value.Length);
            Assert.Equal(slug, result.Value);
        }

        [Theory]
        [InlineData("ab")]
        [InlineData("a")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void Create_RejectsTooShortOrEmptySlug(string? slug)
        {
            Assert.Throws<ArgumentException>(() => TenantSlug.Create(slug!));
        }

        [Fact]
        public void Create_RejectsTooLongSlug()
        {
            var slug = "a" + new string('b', 39) + "z";

            Assert.Throws<ArgumentException>(() => TenantSlug.Create(slug));
        }

        [Theory]
        [InlineData("Abc")]
        [InlineData("ABC")]
        [InlineData("aBc")]
        public void Create_RejectsUppercaseSlug(string slug)
        {
            Assert.Throws<ArgumentException>(() => TenantSlug.Create(slug));
        }

        [Theory]
        [InlineData("ab_cd")]
        [InlineData("ab cd")]
        [InlineData("ab.cd")]
        [InlineData("ab/cd")]
        public void Create_RejectsInvalidCharacters(string slug)
        {
            Assert.Throws<ArgumentException>(() => TenantSlug.Create(slug));
        }

        [Theory]
        [InlineData("-abc")]
        [InlineData("abc-")]
        [InlineData("a--b")]
        [InlineData("-")]
        public void Create_RejectsBadHyphenPlacement(string slug)
        {
            Assert.Throws<ArgumentException>(() => TenantSlug.Create(slug));
        }

        [Theory]
        [InlineData("abßd")]
        [InlineData("ab café")]
        [InlineData("日本")]
        public void Create_RejectsNonAsciiSlug(string slug)
        {
            Assert.Throws<ArgumentException>(() => TenantSlug.Create(slug));
        }

        [Theory]
        [InlineData("admin")]
        [InlineData("ADMIN")]
        [InlineData("Admin")]
        [InlineData("api")]
        [InlineData("API")]
        [InlineData("app")]
        [InlineData("www")]
        [InlineData("t")]
        [InlineData("login")]
        [InlineData("LOGOUT")]
        [InlineData("register")]
        [InlineData("SIGNUP")]
        [InlineData("onboarding")]
        [InlineData("static")]
        [InlineData("assets")]
        [InlineData("health")]
        [InlineData("status")]
        [InlineData("docs")]
        [InlineData("swagger")]
        [InlineData("openapi")]
        [InlineData("support")]
        [InlineData("billing")]
        [InlineData("platform")]
        [InlineData("system")]
        [InlineData("root")]
        [InlineData("null")]
        [InlineData("undefined")]
        public void Create_RejectsReservedWord(string slug)
        {
            Assert.Throws<ArgumentException>(() => TenantSlug.Create(slug));
        }
    }
}
