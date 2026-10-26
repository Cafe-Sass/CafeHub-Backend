using System.Text.RegularExpressions;
using CafeHub.Infrastructure.Provisioning;

namespace CafeHub.UnitTests.Provisioning.Infrastructure
{
    public sealed class TenantCredentialDeriverTests
    {
        private static readonly byte[] MasterKey = new byte[32];
        private static readonly byte[] OtherMasterKey = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        private static readonly Guid TenantId = Guid.CreateVersion7();
        private static readonly Guid OtherTenantId = Guid.CreateVersion7();

        private readonly TenantCredentialDeriver _deriver = new();

        [Fact]
        public void Derive_MatchesThePreRefactorGoldenValue()
        {
            var expected = "TYV8Mje_Z1omFZ5y3sF9NgD7zf3moES-u9gO8ns70G8";
            var actual = _deriver.Derive(
                Enumerable.Range(0, 32).Select(value => (byte)value).ToArray(),
                Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));

            Assert.True(
                string.Equals(expected, actual, StringComparison.Ordinal),
                "the shared deriver should preserve the provisioning credential format");
        }

        [Fact]
        public void Derive_IsDeterministic_ForTheSameTenantAndKey()
        {
            Assert.Equal(_deriver.Derive(MasterKey, TenantId), _deriver.Derive(MasterKey, TenantId));
        }

        [Fact]
        public void Derive_DiffersPerTenant()
        {
            Assert.NotEqual(_deriver.Derive(MasterKey, TenantId), _deriver.Derive(MasterKey, OtherTenantId));
        }

        [Fact]
        public void Derive_DiffersPerMasterKey()
        {
            Assert.NotEqual(_deriver.Derive(MasterKey, TenantId), _deriver.Derive(OtherMasterKey, TenantId));
        }

        [Fact]
        public void Derive_ProducesBase64UrlTextThatCarriesNoCharactersUnsafeEmbeddableAsASqlLiteral()
        {
            var derived = _deriver.Derive(MasterKey, TenantId);

            Assert.Matches(new Regex("^[A-Za-z0-9_-]{43}$"), derived);
        }

        [Fact]
        public void Derive_RejectsAnEmptyMasterKey()
        {
            Assert.Throws<ArgumentException>(() => _deriver.Derive([], TenantId));
        }

        [Fact]
        public void Derive_NeverEchoesTheDerivedValueThroughAnException()
        {
            var derived = _deriver.Derive(MasterKey, TenantId);

            var exception = Record.Exception(() => _deriver.Derive(Array.Empty<byte>(), TenantId));

            Assert.NotNull(exception);
            Assert.DoesNotContain(derived, exception!.ToString(), StringComparison.Ordinal);
        }
    }
}
