using CafeHub.Infrastructure.Persistence.Tenant.Migrations;
using CafeHub.Infrastructure.Provisioning;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CafeHub.UnitTests.Provisioning.Persistence
{
    public sealed class TenantEmailBackfillTests
    {
        private static readonly Guid UserId = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");

        [Fact]
        public void RowDigestBindsTheUserAndRawEmailToTheCanonicalEmail()
        {
            var digest = TenantEmailBackfill.CreateRowDigest(UserId, "  EXAMPLE@Example.Test  ");

            Assert.NotNull(digest);
            Assert.Equal("508752f43f857e3ad2a7da38cd3bab397952735ee1ea3ad3c2f29fa372de9988", Convert.ToHexStringLower(digest));
        }

        [Theory]
        [InlineData(null)]
        [InlineData(" ")]
        [InlineData("missing-at")]
        [InlineData("@example.test")]
        [InlineData("missing-domain@")]
        [InlineData("multiple@@example.test")]
        public void InvalidEmailCannotContributeAProof(string? email) =>
            Assert.Null(TenantEmailBackfill.CreateRowDigest(UserId, email));

        [Fact]
        public void UsesTheLoginLengthLimitInUtf16CodeUnits()
        {
            Assert.NotNull(TenantEmailBackfill.CreateRowDigest(UserId, string.Concat(Enumerable.Repeat("😀", 126)) + "@a"));
            Assert.Null(TenantEmailBackfill.CreateRowDigest(UserId, string.Concat(Enumerable.Repeat("😀", 127)) + "@a"));
        }

        [Fact]
        public void MigrationEmbedsTheTransactionalBackfillWithTheProofGuard()
        {
            var operation = Assert.IsType<SqlOperation>(Assert.Single(new NormalizeTenantUserEmail().UpOperations));

            Assert.False(operation.SuppressTransaction);
            Assert.Contains("current_setting('cafehub.tenant_email_backfill_proof', true)", operation.Sql, StringComparison.Ordinal);
            Assert.Contains("CREATE UNIQUE INDEX uq_tenant_users_normalized_email", operation.Sql, StringComparison.Ordinal);
        }
    }
}
