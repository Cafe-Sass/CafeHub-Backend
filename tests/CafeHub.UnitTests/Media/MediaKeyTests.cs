using CafeHub.Application.Media;

namespace CafeHub.UnitTests.Media
{
    public sealed class MediaKeyTests
    {
        private const string ValidId = "0123456789abcdef0123456789abcdef";

        [Fact]
        public void Create_ForPlatform_ProducesAReversibleKey()
        {
            var key = MediaKey.Create(MediaScope.Platform);

            Assert.StartsWith("platform/", key.Value, StringComparison.Ordinal);
            Assert.True(key.Scope.IsPlatform);
            Assert.True(MediaKey.TryParse(key.Value, out var parsed));
            Assert.NotNull(parsed);
            Assert.True(parsed.Scope.IsPlatform);
            Assert.Equal(key.Value, parsed.Value);
        }

        [Fact]
        public void Create_ForTenant_UsesTheTenantPrefix()
        {
            var tenantId = Guid.NewGuid();

            var key = MediaKey.Create(MediaScope.ForTenant(tenantId));

            Assert.StartsWith($"tenants/{tenantId:D}/", key.Value, StringComparison.Ordinal);
            Assert.True(MediaKey.TryParse(key.Value, out var parsed));
            Assert.Equal(tenantId, parsed!.Scope.TenantId);
            Assert.False(parsed.Scope.IsPlatform);
        }

        [Fact]
        public void Create_Twice_GeneratesDifferentIds()
        {
            var first = MediaKey.Create(MediaScope.Platform);
            var second = MediaKey.Create(MediaScope.Platform);

            Assert.NotEqual(first.Id, second.Id);
            Assert.NotEqual(first.Value, second.Value);
        }

        [Fact]
        public void ForTenant_WithEmptyTenant_Throws()
        {
            Assert.Throws<ArgumentException>(() => MediaScope.ForTenant(Guid.Empty));
        }

        [Theory]
        [InlineData($"platform/{ValidId}")]
        [InlineData("tenants/8f3c2f0a-1111-4a2b-9c3d-1234567890ab/" + ValidId)]
        public void TryParse_WithAValidKey_Succeeds(string value)
        {
            Assert.True(MediaKey.TryParse(value, out var key));
            Assert.Equal(value, key!.Value);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("platform/")]
        [InlineData("/" + "platform/" + ValidId)]
        [InlineData("platform/../" + ValidId)]
        [InlineData("platform/..")]
        [InlineData("platform/" + ValidId + "/..")]
        [InlineData("platform\\" + ValidId)]
        [InlineData("platform/" + ValidId + "\\")]
        [InlineData("platform/" + ValidId + "\0")]
        [InlineData("platform/" + ValidId + "/")]
        [InlineData("platform/" + ValidId + "/extra")]
        [InlineData("platform/" + ValidId + "x")]
        [InlineData("platform/0123456789ABCDEF0123456789ABCDEF")]
        [InlineData("platform/0123456789abcdef0123456789abcde")]
        [InlineData("platform/0123456789abcdef0123456789abcdeg")]
        [InlineData("media/" + ValidId)]
        [InlineData("tenants//" + ValidId)]
        [InlineData("tenants/not-a-guid/" + ValidId)]
        [InlineData("tenants/00000000-0000-0000-0000-000000000000/" + ValidId)]
        [InlineData("tenants/8F3C2F0A-1111-4A2B-9C3D-1234567890AB/" + ValidId)]
        [InlineData("tenants/8f3c2f0a-1111-4a2b-9c3d-1234567890ab")]
        public void TryParse_WithAnUnsafeOrMalformedValue_Fails(string? value)
        {
            Assert.False(MediaKey.TryParse(value, out var key));
            Assert.Null(key);
        }
    }
}
