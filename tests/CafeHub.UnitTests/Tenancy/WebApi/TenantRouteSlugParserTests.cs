using CafeHub.WebApi.Tenancy;
using Microsoft.AspNetCore.Http;

namespace CafeHub.UnitTests.Tenancy.WebApi
{
    public sealed class TenantRouteSlugParserTests
    {
        private const string OverLongSlug = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        public static TheoryData<string, string?> ParserTable => new()
        {
            { "/t/my-cafe/x", "my-cafe" },
            { "/t/my-cafe", "my-cafe" },
            { "/t/my-cafe/", "my-cafe" },
            { "/t/my-cafe/x/", "my-cafe" },
            { "/T/Cafe-One/x", "Cafe-One" },
            { "/T/MY-CAFE", "MY-CAFE" },
            { "/t/" + new string('a', 40) + "/x", new string('a', 40) },
            { "/api/v1/plans", null },
            { "/tenant/my-cafe/x", null },
            { "/ta/my-cafe/x", null },
            { "/t", null },
            { "/t/", null },
            { "/t//x", null },
            { "//t/x", null },
            { "", null },
            { "/t/" + OverLongSlug + "/x", null },
            { "/t/%2e/x", null },
            { "/t/a.b/x", null },
            { "/t/my cafe/x", null },
            { "/t/\tcafe/x", null },
        };

        [Theory]
        [MemberData(nameof(ParserTable))]
        public void TryGetSlug_YieldsTheSlugOnlyForTenantPaths(string path, string? expectedSlug)
        {
            var parsed = TenantRouteSlugParser.TryGetSlug(new PathString(path), out var slug);

            if (expectedSlug is null)
            {
                Assert.False(parsed);
                Assert.Equal(string.Empty, slug);
            }
            else
            {
                Assert.True(parsed);
                Assert.Equal(expectedSlug, slug);
            }
        }

        [Fact]
        public void TryGetSlug_NeverReadsTheRegistry()
        {
            var parsed = TenantRouteSlugParser.TryGetSlug(new PathString("/t/my-cafe/x"), out var slug);

            Assert.True(parsed);
            Assert.Equal("my-cafe", slug);
        }
    }
}
