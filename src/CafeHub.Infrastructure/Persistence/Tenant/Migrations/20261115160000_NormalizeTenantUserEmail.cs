using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeHub.Infrastructure.Persistence.Tenant.Migrations
{
    public partial class NormalizeTenantUserEmail : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            using var script = typeof(NormalizeTenantUserEmail).Assembly
                .GetManifestResourceStream("CafeHub.NormalizeTenantUserEmail.Backfill.sql")
                ?? throw new InvalidOperationException("The tenant email backfill SQL is unavailable.");
            using var reader = new StreamReader(script);
            migrationBuilder.Sql(reader.ReadToEnd());
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex("uq_tenant_users_normalized_email", "tenant_users");
            migrationBuilder.CreateIndex("ix_tenant_users_normalized_email", "tenant_users", "normalized_email");
        }
    }
}
