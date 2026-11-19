using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CafeHub.UnitTests.Tenants.Persistence
{
    public sealed class Task47RemediationMigrationTests
    {
        [Fact]
        public void RemediationMigrationsPreserveLegacyStationAndGuardTagFinalizationAndDowngrade()
        {
            using var context = new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>()
                .UseNpgsql("Host=192.0.2.1;Database=offline").Options);
            var migrator = context.GetService<IMigrator>();
            const string legacy = "20261116122028_AddMenuProducts";
            const string expansion = "20261117233117_ExpandTask47ProductRemediation";
            const string finalization = "20261117233501_FinalizeTask47ProductTagIdentity";

            var expand = migrator.GenerateScript(legacy, expansion);
            var finalize = migrator.GenerateScript(expansion, finalization);
            var down = migrator.GenerateScript(finalization, legacy);

            Assert.Contains("ALTER TABLE product_tags ADD id uuid;", expand);
            Assert.Contains("REFERENCES stations (branch_id, id) ON DELETE RESTRICT", expand);
            Assert.Contains("ON product_station_assignments (product_id, branch_id) WHERE ended_at IS NULL", expand);
            Assert.DoesNotContain("DROP COLUMN station_id", expand + finalize + down);
            Assert.DoesNotContain("INSERT INTO product_routing_readiness", expand + finalize);

            AssertBefore(finalize, "LOCK TABLE product_tags IN ACCESS EXCLUSIVE MODE", "RAISE EXCEPTION");
            AssertBefore(finalize, "RAISE EXCEPTION", "ALTER COLUMN id SET NOT NULL");
            Assert.Contains("ck_product_tags_normalized_label", finalize);
            Assert.Contains("DROP CONSTRAINT ck_product_tags_normalized_label", down);
            Assert.Contains("PRIMARY KEY (id)", finalize);
            Assert.Contains("ON product_tags (product_id, tag) WHERE status = 'Active'", finalize);

            AssertBefore(down, "tag history or cutover state prevents a lossless downgrade", "PRIMARY KEY (product_id, tag)");
            AssertBefore(down, "remediation data must be preserved", "DROP TABLE product_station_assignments");
            AssertBefore(down, "remediation data must be preserved", "DROP COLUMN id");
            Assert.False(migrator.HasPendingModelChanges());
        }

        private static void AssertBefore(string sql, string earlier, string later)
        {
            var first = sql.IndexOf(earlier, StringComparison.Ordinal);
            var second = sql.IndexOf(later, StringComparison.Ordinal);
            Assert.True(first >= 0 && second > first, $"Expected '{earlier}' before '{later}'.");
        }
    }
}
