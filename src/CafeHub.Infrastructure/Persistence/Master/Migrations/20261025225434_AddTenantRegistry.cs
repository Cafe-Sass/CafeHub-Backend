using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeHub.Infrastructure.Persistence.Master.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    onboarding_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "text", nullable: false),
                    lifecycle_status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenants", x => x.id);
                    table.CheckConstraint("ck_tenants_lifecycle_status", "lifecycle_status IN ('Provisioning', 'Active', 'Suspended', 'Terminated')");
                    table.CheckConstraint("ck_tenants_slug_format", "slug ~ '^[a-z0-9][a-z0-9-]{1,38}[a-z0-9]$'");
                    table.CheckConstraint("ck_tenants_slug_lowercase", "slug = lower(slug)");
                    table.CheckConstraint("ck_tenants_slug_reserved", "slug NOT IN ('admin','api','t','www','app','static','assets','health','login','signup','support','platform')");
                    table.ForeignKey(
                        name: "fk_tenants_created_by",
                        column: x => x.created_by,
                        principalTable: "platform_admins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tenants_onboarding_request_id",
                        column: x => x.onboarding_request_id,
                        principalTable: "onboarding_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tenants_updated_by",
                        column: x => x.updated_by,
                        principalTable: "platform_admins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tenant_databases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    secret_reference = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_databases", x => x.id);
                    table.CheckConstraint("ck_tenant_databases_secret_reference_format", "secret_reference ~ '^[a-z0-9][a-z0-9-]*$'");
                    table.CheckConstraint("ck_tenant_databases_secret_reference_length", "length(secret_reference) >= 1 AND length(secret_reference) <= 128");
                    table.CheckConstraint("ck_tenant_databases_status", "status IN ('Pending', 'Provisioned', 'Failed')");
                    table.ForeignKey(
                        name: "fk_tenant_databases_created_by",
                        column: x => x.created_by,
                        principalTable: "platform_admins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tenant_databases_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tenant_databases_updated_by",
                        column: x => x.updated_by,
                        principalTable: "platform_admins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_databases_created_by",
                table: "tenant_databases",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_databases_updated_by",
                table: "tenant_databases",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "uq_tenant_databases_secret_reference",
                table: "tenant_databases",
                column: "secret_reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_tenant_databases_tenant_id",
                table: "tenant_databases",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenants_created_by",
                table: "tenants",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_tenants_updated_by",
                table: "tenants",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "uq_tenants_onboarding_request_id",
                table: "tenants",
                column: "onboarding_request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_tenants_slug",
                table: "tenants",
                column: "slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenant_databases");

            migrationBuilder.DropTable(
                name: "tenants");
        }
    }
}
