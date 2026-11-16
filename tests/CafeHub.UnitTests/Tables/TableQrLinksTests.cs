using CafeHub.Application.Tables.DTOs;
using CafeHub.Application.Tenancy;
using CafeHub.UnitTests.Branches;
using CafeHub.WebApi.Controllers;

namespace CafeHub.UnitTests.Tables
{
    public sealed class TableQrLinksTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("/app")]
        [InlineData("https://")]
        [InlineData("ftp://example.test")]
        [InlineData("http://example.test")]
        [InlineData("http://localhost")]
        [InlineData("https://a:b@example.test/")]
        [InlineData("https://@example.test")]
        [InlineData("https://example.test/app")]
        [InlineData("https://example.test/app/..")]
        [InlineData("https://example.test//")]
        [InlineData("https://example.test/?")]
        [InlineData("https://example.test/#")]
        [InlineData("https://example.test\\")]
        public void ParseOrigin_RejectsInvalidOrNonHttpsConfiguration(string? origin)
        {
            var exception = Assert.Throws<InvalidOperationException>(() => TableQrLinks.ParseOrigin(origin, false));

            Assert.Contains(TableQrLinks.ConfigurationKey, exception.Message);
            Assert.DoesNotContain("a:b", exception.Message);
        }

        [Theory]
        [InlineData("http://localhost:5173/")]
        [InlineData("http://127.0.0.1/")]
        [InlineData("http://[::1]/")]
        public void ParseOrigin_AllowsHttpLoopbackOnlyInDevelopment(string origin)
        {
            Assert.Equal(origin, TableQrLinks.ParseOrigin(origin, true).AbsoluteUri);
            Assert.Throws<InvalidOperationException>(() => TableQrLinks.ParseOrigin(origin, false));
            Assert.Throws<InvalidOperationException>(() => TableQrLinks.ParseOrigin("http://example.test", true));
        }

        [Theory]
        [InlineData("https://public.example.test:8443")]
        [InlineData("https://public.example.test:8443/")]
        public void Create_UsesTheResolvedSlugAndConfiguredOrigin(string origin)
        {
            var tableId = Guid.NewGuid();
            var context = new StubTenantContextAccessor(TenantContext.Create(Guid.NewGuid(), "test-reference", true, "canonical-cafe"));
            var links = new TableQrLinks(TableQrLinks.ParseOrigin(origin, false), context);

            var link = links.Create(new TableQrData(tableId, 21, "Terrace", "Abcdefghijklmnopqrstuv", 123));

            Assert.Equal(tableId, link.TableId);
            Assert.Equal(21, link.Number);
            Assert.Equal("Terrace", link.Label);
            Assert.Equal("/t/canonical-cafe/q/Abcdefghijklmnopqrstuv", link.Path);
            Assert.Equal($"https://public.example.test:8443{link.Path}", link.Url);
            Assert.Equal("123", link.ConcurrencyToken);
        }
    }
}
