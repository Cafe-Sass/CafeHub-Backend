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
