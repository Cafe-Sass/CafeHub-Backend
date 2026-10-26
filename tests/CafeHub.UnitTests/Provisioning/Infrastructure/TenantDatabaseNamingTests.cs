using System.Text.RegularExpressions;
using CafeHub.Application.Provisioning;

namespace CafeHub.UnitTests.Provisioning.Infrastructure
{
    public sealed class TenantDatabaseNamingTests
    {
        private static readonly string MaxLengthSlug = "a" + new string('b', 38) + "c";

        [Fact]
        public void DatabaseName_IsDeterministic()
        {
            Assert.Equal(
                TenantDatabaseNaming.DatabaseName("cool-cafe"),
                TenantDatabaseNaming.DatabaseName("cool-cafe"));
        }

        [Fact]
        public void DatabaseName_ReplacesHyphensWithUnderscores_AsTheCommandsDocumentDefines()
        {
            Assert.Equal("cafehub_tenant_cool_cafe", TenantDatabaseNaming.DatabaseName("cool-cafe"));
        }

        [Fact]
        public void DatabaseName_MatchesTheAllowedPattern()
        {
            Assert.True(TenantDatabaseNaming.IsValidDatabaseName(
                TenantDatabaseNaming.DatabaseName("cool-cafe")));
        }

        [Fact]
        public void DatabaseName_DiffersPerTenant()
        {
            Assert.NotEqual(
                TenantDatabaseNaming.DatabaseName("cool-cafe"),
                TenantDatabaseNaming.DatabaseName("bean-there"));
        }

        [Fact]
        public void OwnerAndApplicationRoleNames_AreDerived_AndStayWithinSixtyThreeCharacters()
        {
            var databaseName = TenantDatabaseNaming.DatabaseName(MaxLengthSlug);

            Assert.True(databaseName.Length <= 63);
            Assert.True(TenantDatabaseNaming.OwnerRoleName(databaseName).Length <= 63);
            Assert.True(TenantDatabaseNaming.ApplicationRoleName(databaseName).Length <= 63);
            Assert.EndsWith("_owner", TenantDatabaseNaming.OwnerRoleName(databaseName), StringComparison.Ordinal);
            Assert.EndsWith("_app", TenantDatabaseNaming.ApplicationRoleName(databaseName), StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("cool-cafe")]
        [InlineData("a-bc")]
        [InlineData("cafe123-brown")]
        public void IsValidTenantSlug_AcceptsCommandDocumentSlugs(string tenantSlug)
        {
            Assert.True(TenantDatabaseNaming.IsValidTenantSlug(tenantSlug));
        }

        [Theory]
        [InlineData("")]
        [InlineData("ab")]
        [InlineData("cool_cafe")]
        [InlineData("-nope")]
        [InlineData("nope-")]
        [InlineData("Bad-Slug")]
        public void IsValidTenantSlug_RejectsMalformedSlugs(string tenantSlug)
        {
            Assert.False(TenantDatabaseNaming.IsValidTenantSlug(tenantSlug));
        }

        [Theory]
        [InlineData("1abc")]
        [InlineData("Bad_Name")]
        [InlineData("has-dash")]
        [InlineData("has space")]
        [InlineData("")]
        public void IsValidDatabaseName_RejectsMalformedNames(string databaseName)
        {
            Assert.False(TenantDatabaseNaming.IsValidDatabaseName(databaseName));
        }

        [Fact]
        public void DatabaseNamePattern_IsSixtyThreeCharacterSafe()
        {
            Assert.Matches(new Regex(TenantDatabaseNaming.DatabaseNamePattern), "cafehub_tenant_cool_cafe");
            Assert.DoesNotMatch(new Regex(TenantDatabaseNaming.DatabaseNamePattern), new string('a', 64));
        }
    }
}
